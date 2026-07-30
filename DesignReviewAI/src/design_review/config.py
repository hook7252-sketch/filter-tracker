"""검토 프로그램 전역 설정: 판정 카테고리, 색상, 기본값."""
from __future__ import annotations

# 판정 등급 (심각도 내림차순). 페이지별검토·정합성검증·구조검토 시트에서 공통으로 사용.
VERDICT_LEVELS = [
    "승인보류",
    "재계산",
    "중대보완",
    "조건부확인",
    "일반보완",
    "확인",
    "공백",
]

# 판정별 우선순위 (숫자가 작을수록 심각). 여러 이슈가 겹치는 페이지의 최종 판정 결정에 사용.
VERDICT_PRIORITY = {v: i for i, v in enumerate(VERDICT_LEVELS)}

# 판정별 강조색 (엑셀 조건부 서식용, ARGB)
VERDICT_FILL_COLORS = {
    "승인보류": "FFF4CCCC",
    "재계산": "FFFCE5CD",
    "중대보완": "FFFFF2CC",
    "조건부확인": "FFFFF9B0",
    "일반보완": "FFD9EAD3",
    "확인": "FFD0E0E3",
    "공백": "FFEFEFEF",
}

COST_LINKAGE_LEVELS = ["확정", "조건부", "대안", "미산정", "소액", "미가격", "없음"]

DEFAULT_MODEL = "claude-sonnet-5"
DEFAULT_BATCH_SIZE = 4
# 페이지 텍스트가 이 글자 수 미만이면 도면/스캔 페이지로 간주하고 이미지(비전)를 함께 전달한다.
IMAGE_FALLBACK_TEXT_THRESHOLD = 120
RENDER_DPI = 150
MAX_IMAGE_LONG_SIDE = 1568  # Anthropic 권장 이미지 크기 상한(px)

MAX_RETRIES = 5
RETRY_BASE_DELAY_SEC = 2.0

PAGE_CATEGORY_HINTS = [
    "표지·목차", "설계설명서", "일반시방서", "특별시방서", "설계내역서",
    "단가산출서", "일위대가", "중기손료", "자재단가", "노임단가", "견적서",
    "수량산출서", "도면", "구조계산서", "기타",
]
