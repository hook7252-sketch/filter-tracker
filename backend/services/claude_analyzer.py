"""
Claude API를 이용한 발주도서 분석 서비스
- 설계도면 ↔ 수량산출서 ↔ 내역서 상호 비교
- 건설공사 표준품셈 기준 검토
- 이상 항목 탐지 및 리포트 생성
"""
import json
import logging
from typing import Dict, Any, List, Optional

import anthropic

logger = logging.getLogger(__name__)

client = anthropic.Anthropic()

SYSTEM_PROMPT = """당신은 건설공사 발주도서 검토 전문가입니다.
건설기술진흥법, 건설공사 표준품셈, 공사 원가계산 기준 등을 숙지하고 있습니다.

분석 시 다음을 중점 검토하세요:
1. 설계도면 ↔ 수량산출서 정합성 (도면의 치수/규격과 산출된 수량 일치 여부)
2. 수량산출서 ↔ 내역서 정합성 (산출된 수량과 내역서 계상 수량 일치 여부)
3. 내역서 단가의 적정성 (표준품셈 기준 대비 과다/과소 계상)
4. 누락 항목 여부
5. 산식 오류 및 계산 착오
6. 규격/단위 불일치
7. 공종 분류 오류

응답은 반드시 JSON 형식으로 반환하세요."""


def analyze_documents(
    documents: List[Dict[str, Any]],
    analysis_types: List[str],
) -> Dict[str, Any]:
    """
    업로드된 발주도서를 Claude로 분석

    documents: [{"id": ..., "doc_type": ..., "filename": ..., "content": ...}, ...]
    analysis_types: ["cross_check", "standard_check", "quantity_check"]
    """

    # 문서 유형별 분류
    drawing = next((d for d in documents if d["doc_type"] == "drawing"), None)
    quantity = next((d for d in documents if d["doc_type"] == "quantity"), None)
    boq = next((d for d in documents if d["doc_type"] == "boq"), None)

    # 분석 프롬프트 구성
    prompt = _build_analysis_prompt(drawing, quantity, boq, analysis_types)

    logger.info(f"Claude 분석 시작 - 문서 수: {len(documents)}, 분석 유형: {analysis_types}")

    # Claude API 호출 (스트리밍 + adaptive thinking)
    with client.messages.stream(
        model="claude-opus-4-6",
        max_tokens=8000,
        thinking={"type": "adaptive"},
        system=SYSTEM_PROMPT,
        messages=[{"role": "user", "content": prompt}],
    ) as stream:
        response = stream.get_final_message()

    # 응답 파싱
    return _parse_analysis_response(response)


def _build_analysis_prompt(
    drawing: Optional[Dict],
    quantity: Optional[Dict],
    boq: Optional[Dict],
    analysis_types: List[str],
) -> str:
    """Claude에 전달할 분석 프롬프트 구성"""

    parts = ["# 발주도서 검토 요청\n"]

    if drawing:
        parts.append(f"## 설계도면 ({drawing['filename']})")
        parts.append(drawing["content"][:12000])
        parts.append("")

    if quantity:
        parts.append(f"## 수량산출서 ({quantity['filename']})")
        parts.append(quantity["content"][:12000])
        parts.append("")

    if boq:
        parts.append(f"## 내역서 ({boq['filename']})")
        parts.append(boq["content"][:12000])
        parts.append("")

    parts.append("## 검토 요청 사항")

    if "cross_check" in analysis_types:
        parts.append("""
1. **상호 비교 검토**: 업로드된 문서 간 수량, 규격, 단위 등의 정합성을 검토하세요.
   - 설계도면의 치수/규격이 수량산출서에 올바르게 반영되었는지
   - 수량산출서의 수량이 내역서에 올바르게 반영되었는지
   - 불일치 항목이 있으면 구체적인 수치와 함께 지적해주세요""")

    if "standard_check" in analysis_types:
        parts.append("""
2. **표준품셈 기준 검토**: 건설공사 표준품셈 및 공사비 산정 기준과 비교하세요.
   - 노무비, 재료비, 경비의 계상이 표준품셈 범위 내인지
   - 할증률, 손율 등의 적용이 적절한지
   - 비목별 단가의 적정성""")

    if "quantity_check" in analysis_types:
        parts.append("""
3. **수량 검토**: 수량 산출의 정확성을 검토하세요.
   - 산출 공식의 오류 여부
   - 단위 변환 오류
   - 누락된 수량 항목""")

    parts.append("""
## 응답 형식
반드시 아래 JSON 형식으로만 응답하세요 (다른 텍스트 없이):

```json
{
  "summary": "전체 검토 결과 요약 (2-3문장)",
  "statistics": {
    "total_issues": 0,
    "errors": 0,
    "warnings": 0,
    "infos": 0,
    "documents_reviewed": 0
  },
  "issues": [
    {
      "level": "error|warning|info",
      "category": "수량불일치|단가오류|규격불일치|누락항목|계산오류|표준품셈초과|기타",
      "description": "문제 설명",
      "source_doc": "출처 문서명",
      "target_doc": "비교 문서명 (해당시)",
      "detail": "상세 내용 (수치 포함)",
      "recommendation": "시정 권고사항"
    }
  ],
  "cross_comparison": {
    "drawing_vs_quantity": {
      "consistent": true/false,
      "items_checked": 0,
      "mismatches": []
    },
    "quantity_vs_boq": {
      "consistent": true/false,
      "items_checked": 0,
      "mismatches": []
    }
  },
  "standard_check": {
    "compliant": true/false,
    "violations": [],
    "notes": []
  }
}
```""")

    return "\n".join(parts)


def _parse_analysis_response(response: anthropic.types.Message) -> Dict[str, Any]:
    """Claude 응답에서 JSON 파싱"""

    raw_text = ""
    for block in response.content:
        if block.type == "text":
            raw_text = block.text
            break

    # JSON 블록 추출
    json_text = raw_text
    if "```json" in raw_text:
        start = raw_text.find("```json") + 7
        end = raw_text.find("```", start)
        json_text = raw_text[start:end].strip()
    elif "```" in raw_text:
        start = raw_text.find("```") + 3
        end = raw_text.find("```", start)
        json_text = raw_text[start:end].strip()

    try:
        parsed = json.loads(json_text)
        parsed["raw_analysis"] = raw_text
        return parsed
    except json.JSONDecodeError as e:
        logger.warning(f"JSON 파싱 실패, 원본 텍스트 반환: {e}")
        # 파싱 실패 시 기본 구조 반환
        return {
            "summary": raw_text[:500] if raw_text else "분석 결과를 파싱하지 못했습니다.",
            "statistics": {"total_issues": 0, "errors": 0, "warnings": 0, "infos": 0, "documents_reviewed": 0},
            "issues": [],
            "cross_comparison": None,
            "standard_check": None,
            "raw_analysis": raw_text,
        }


def quick_parse_document(content: str, doc_type: str, filename: str) -> Dict[str, Any]:
    """단일 문서 빠른 분석 (업로드 직후 구조 파악)"""

    doc_type_kr = {"drawing": "설계도면", "quantity": "수량산출서", "boq": "내역서"}.get(
        doc_type, doc_type
    )

    prompt = f"""다음 {doc_type_kr} 파일({filename})의 내용을 분석하여 구조를 파악하세요.

{content[:8000]}

아래 JSON 형식으로 응답하세요:
{{
  "doc_structure": "문서 구조 요약",
  "key_items": ["주요 항목 1", "주요 항목 2"],
  "detected_type": "{doc_type}",
  "notes": "특이사항"
}}"""

    response = client.messages.create(
        model="claude-opus-4-6",
        max_tokens=1000,
        messages=[{"role": "user", "content": prompt}],
    )

    raw = response.content[0].text if response.content else ""

    try:
        if "```json" in raw:
            start = raw.find("```json") + 7
            end = raw.find("```", start)
            return json.loads(raw[start:end].strip())
        return json.loads(raw)
    except Exception:
        return {"doc_structure": raw, "key_items": [], "notes": ""}
