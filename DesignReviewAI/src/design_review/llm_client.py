"""Claude API 호출 래퍼: 재시도, 이미지 첨부, tool-use 강제 JSON 출력."""
from __future__ import annotations

import os
import time
from typing import Any

from . import config

try:
    import anthropic
except ImportError as exc:  # pragma: no cover
    raise ImportError(
        "anthropic 패키지가 설치되어 있지 않습니다. `pip install anthropic`으로 설치하세요."
    ) from exc


class LLMClient:
    def __init__(self, model: str = config.DEFAULT_MODEL, api_key: str | None = None):
        api_key = api_key or os.environ.get("ANTHROPIC_API_KEY")
        if not api_key:
            raise RuntimeError(
                "ANTHROPIC_API_KEY 환경변수가 설정되어 있지 않습니다. "
                "Claude API 키를 환경변수로 설정한 뒤 다시 실행하세요."
            )
        self.client = anthropic.Anthropic(api_key=api_key)
        self.model = model

    def call_tool(
        self,
        system: str,
        user_content: list[dict[str, Any]] | str,
        tool: dict[str, Any],
        max_tokens: int = 8000,
    ) -> dict[str, Any]:
        """도구 1개를 강제 호출시켜 구조화된 JSON 입력을 받아온다."""
        if isinstance(user_content, str):
            user_content = [{"type": "text", "text": user_content}]

        last_error: Exception | None = None
        for attempt in range(config.MAX_RETRIES):
            try:
                resp = self.client.messages.create(
                    model=self.model,
                    max_tokens=max_tokens,
                    system=system,
                    tools=[tool],
                    tool_choice={"type": "tool", "name": tool["name"]},
                    messages=[{"role": "user", "content": user_content}],
                )
                for block in resp.content:
                    if block.type == "tool_use" and block.name == tool["name"]:
                        return block.input
                raise RuntimeError("응답에서 tool_use 블록을 찾지 못했습니다.")
            except (anthropic.RateLimitError, anthropic.APIStatusError, anthropic.APIConnectionError) as exc:
                last_error = exc
                delay = config.RETRY_BASE_DELAY_SEC * (2 ** attempt)
                time.sleep(delay)
        raise RuntimeError(f"Claude API 호출이 {config.MAX_RETRIES}회 재시도 후에도 실패했습니다: {last_error}")


def build_page_message(page, extra_instruction: str = "") -> list[dict[str, Any]]:
    """PageContent 하나를 API 메시지 content 조각으로 변환 (텍스트 + 필요시 이미지)."""
    parts: list[dict[str, Any]] = [
        {"type": "text", "text": f"--- PDF {page.page}쪽 ---\n{page.text or '(추출된 텍스트 없음, 아래 이미지 참고)'}"}
    ]
    if page.image_b64:
        parts.append(
            {
                "type": "image",
                "source": {
                    "type": "base64",
                    "media_type": page.image_media_type,
                    "data": page.image_b64,
                },
            }
        )
    if extra_instruction:
        parts.append({"type": "text", "text": extra_instruction})
    return parts
