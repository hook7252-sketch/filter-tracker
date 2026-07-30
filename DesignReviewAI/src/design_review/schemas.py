"""데이터 구조 정의 및 Claude tool-use용 JSON 스키마."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Optional

from . import config


# --------------------------------------------------------------------------
# 내부 데이터 클래스
# --------------------------------------------------------------------------

@dataclass
class PageContent:
    """PDF에서 추출한 원본 페이지 정보."""

    page: int
    text: str
    image_b64: Optional[str] = None  # 텍스트가 부족한(도면) 페이지에만 채움
    image_media_type: str = "image/png"


@dataclass
class KeyValue:
    label: str
    value: str
    unit: str = ""


@dataclass
class PageExtract:
    """1차 패스(페이지 추출) 결과. 교차분석의 원재료가 된다."""

    page: int
    category: str
    source_excerpt: str
    key_values: list[KeyValue] = field(default_factory=list)
    notes: str = ""


@dataclass
class PageReview:
    """최종 페이지별검토 시트 한 행."""

    page: int
    category: str
    source_excerpt: str
    review_focus: str
    verdict: str
    detail: str
    required_action: str
    related_pages: str
    issue_ids: str = ""


@dataclass
class ConsistencyIssue:
    """정합성검증 시트 한 행 (이슈 레지스트리)."""

    id: str
    importance: str
    category: str
    item: str
    ref1_pages: str
    ref1_value: str
    ref2_pages: str
    ref2_value: str
    difference: str
    impact_verdict: str
    required_action: str
    cost_linkage: str
    related_pages: str = ""  # 이 이슈가 영향을 주는 페이지 범위 (페이지별검토 판정 역전파용)


@dataclass
class StructuralRow:
    facility: str
    part: str
    drawing_ref: str
    calc_ref: str
    review_result: str
    impact: str
    required_action: str
    verdict: str
    has_opinion: bool = False


@dataclass
class CostRow:
    item: str
    current_qty: float
    review_qty: float
    unit: str
    unit_price: float
    certainty: str  # 확정/조건부/대안
    include_in_total: bool
    basis: str


@dataclass
class MissingQuantityRow:
    item: str
    per_unit_value: Optional[float]
    per_unit_label: str
    total_value: Optional[float]
    total_label: str
    unit: str
    basis: str


@dataclass
class StandardRef:
    field: str
    reference: str
    relevance: str
    url: str
    checked_date: str


@dataclass
class ReviewAnalysis:
    """교차분석 패스 전체 출력."""

    issues: list[ConsistencyIssue] = field(default_factory=list)
    structural_rows: list[StructuralRow] = field(default_factory=list)
    cost_rows: list[CostRow] = field(default_factory=list)
    missing_quantity_rows: list[MissingQuantityRow] = field(default_factory=list)
    standards: list[StandardRef] = field(default_factory=list)
    overall_judgement: str = ""
    review_limitation: str = ""
    consistency_note: str = ""
    cost_note: str = ""


@dataclass
class ProjectMeta:
    project_name: str
    source_filename: str
    review_scope: str
    review_date: str


# --------------------------------------------------------------------------
# Claude tool-use JSON 스키마 (structured output 강제용)
# --------------------------------------------------------------------------

PAGE_EXTRACTION_TOOL = {
    "name": "record_page_extractions",
    "description": "배치로 전달된 각 PDF 페이지의 구분·핵심 식별정보·수치를 구조화하여 기록한다.",
    "input_schema": {
        "type": "object",
        "properties": {
            "pages": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "page": {"type": "integer", "description": "PDF 쪽 번호"},
                        "category": {
                            "type": "string",
                            "description": "문서 구분. 예: " + ", ".join(config.PAGE_CATEGORY_HINTS),
                        },
                        "source_excerpt": {
                            "type": "string",
                            "description": "원문에서 핵심 제목/항목/수치를 발췌 요약 (200자 이내)",
                        },
                        "key_values": {
                            "type": "array",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "label": {"type": "string"},
                                    "value": {"type": "string"},
                                    "unit": {"type": "string"},
                                },
                                "required": ["label", "value"],
                            },
                            "description": "이 페이지에 나온 수량·규격·금액 등 교차검증에 쓸 핵심 수치 목록",
                        },
                        "notes": {
                            "type": "string",
                            "description": "이 페이지 단독으로 관찰되는 특이사항(공란/오탈자/불명확 표현). 없으면 빈 문자열.",
                        },
                    },
                    "required": ["page", "category", "source_excerpt", "key_values", "notes"],
                },
            }
        },
        "required": ["pages"],
    },
}

CROSS_ANALYSIS_TOOL = {
    "name": "record_review_analysis",
    "description": "전체 페이지 추출 결과를 바탕으로 정합성 이슈, 구조검토, 공사비영향, 기준참고, 종합판정을 구조화하여 기록한다.",
    "input_schema": {
        "type": "object",
        "properties": {
            "issues": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "id": {"type": "string", "description": "I01, I02 형식의 고유 ID"},
                        "importance": {"type": "string", "enum": config.VERDICT_LEVELS},
                        "category": {
                            "type": "string",
                            "description": "이슈 분류. 예: 구조, 수량/내역, 내역, 시방/단가, 도면",
                        },
                        "item": {"type": "string"},
                        "ref1_pages": {"type": "string"},
                        "ref1_value": {"type": "string"},
                        "ref2_pages": {"type": "string"},
                        "ref2_value": {"type": "string"},
                        "difference": {"type": "string"},
                        "impact_verdict": {"type": "string"},
                        "required_action": {"type": "string"},
                        "cost_linkage": {"type": "string", "enum": config.COST_LINKAGE_LEVELS},
                        "related_pages": {
                            "type": "string",
                            "description": "이 이슈가 영향을 미치는 전체 페이지 범위 (예: '30,87' 또는 '198~236')",
                        },
                    },
                    "required": [
                        "id", "importance", "category", "item", "ref1_pages", "ref1_value",
                        "ref2_pages", "ref2_value", "difference", "impact_verdict",
                        "required_action", "cost_linkage", "related_pages",
                    ],
                },
            },
            "structural_rows": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "facility": {"type": "string"},
                        "part": {"type": "string"},
                        "drawing_ref": {"type": "string"},
                        "calc_ref": {"type": "string"},
                        "review_result": {"type": "string"},
                        "impact": {"type": "string"},
                        "required_action": {"type": "string"},
                        "verdict": {"type": "string", "enum": config.VERDICT_LEVELS},
                        "has_opinion": {"type": "boolean"},
                    },
                    "required": [
                        "facility", "part", "drawing_ref", "calc_ref", "review_result",
                        "impact", "required_action", "verdict", "has_opinion",
                    ],
                },
            },
            "cost_rows": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "item": {"type": "string"},
                        "current_qty": {"type": "number"},
                        "review_qty": {"type": "number"},
                        "unit": {"type": "string"},
                        "unit_price": {"type": "number"},
                        "certainty": {"type": "string", "enum": ["확정", "조건부", "대안"]},
                        "include_in_total": {"type": "boolean"},
                        "basis": {"type": "string"},
                    },
                    "required": [
                        "item", "current_qty", "review_qty", "unit", "unit_price",
                        "certainty", "include_in_total", "basis",
                    ],
                },
            },
            "missing_quantity_rows": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "item": {"type": "string"},
                        "per_unit_value": {"type": ["number", "null"]},
                        "per_unit_label": {"type": "string"},
                        "total_value": {"type": ["number", "null"]},
                        "total_label": {"type": "string"},
                        "unit": {"type": "string"},
                        "basis": {"type": "string"},
                    },
                    "required": [
                        "item", "per_unit_value", "per_unit_label",
                        "total_value", "total_label", "unit", "basis",
                    ],
                },
            },
            "standards": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "field": {"type": "string"},
                        "reference": {"type": "string"},
                        "relevance": {"type": "string"},
                        "url": {
                            "type": "string",
                            "description": "정확한 URL을 모르면 빈 문자열로 두고 relevance에 '검색 필요'라고 명시",
                        },
                    },
                    "required": ["field", "reference", "relevance", "url"],
                },
            },
            "overall_judgement": {
                "type": "string",
                "description": "종합요약 시트 상단에 들어갈 2~4문장의 종합판정",
            },
            "review_limitation": {
                "type": "string",
                "description": "본 자동검토의 한계(현장조사·지반조사 등 미반영 사항 고지)",
            },
            "consistency_note": {"type": "string", "description": "정합성검증 표 상단 부제 설명"},
            "cost_note": {"type": "string", "description": "공사비영향 표 상단 부호 기준 설명"},
        },
        "required": [
            "issues", "structural_rows", "cost_rows", "missing_quantity_rows",
            "standards", "overall_judgement", "review_limitation",
        ],
    },
}

PAGE_VERDICT_TOOL = {
    "name": "record_page_verdicts",
    "description": "이슈 레지스트리를 반영해 각 페이지의 최종 판정·검토초점·세부결과·요구조치·이슈ID를 기록한다.",
    "input_schema": {
        "type": "object",
        "properties": {
            "pages": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "page": {"type": "integer"},
                        "review_focus": {"type": "string"},
                        "verdict": {"type": "string", "enum": config.VERDICT_LEVELS},
                        "detail": {"type": "string"},
                        "required_action": {"type": "string"},
                        "related_pages": {"type": "string"},
                        "issue_ids": {
                            "type": "string",
                            "description": "관련 이슈ID를 세미콜론으로 연결 (예: 'I02' 또는 'I04;I05'). 없으면 빈 문자열.",
                        },
                    },
                    "required": [
                        "page", "review_focus", "verdict", "detail",
                        "required_action", "related_pages", "issue_ids",
                    ],
                },
            }
        },
        "required": ["pages"],
    },
}
