"""
발주도서 직접 비교 분석 서비스 (로컬, AI 없이)
- 설계도면 ↔ 수량산출서 ↔ 내역서 표 데이터 직접 비교
- 수량/단위 불일치 탐지
- 누락 항목 탐지
"""
import re
import logging
from typing import Dict, Any, List, Optional, Tuple
from difflib import SequenceMatcher

logger = logging.getLogger(__name__)

# 컬럼 식별 키워드
COLUMN_KEYWORDS = {
    "code": ["코드", "번호", "no", "항목번호", "공종코드", "품목코드", "순번", "code"],
    "name": ["품명", "공종명", "명칭", "항목명", "공사명", "공종", "내용", "품목", "name"],
    "unit": ["단위", "unit"],
    "quantity": ["수량", "물량", "qty", "quantity"],
    "unit_price": ["단가", "단위가격", "unit price"],
    "total": ["금액", "합계", "계", "total"],
}


def _normalize_text(text: str) -> str:
    """텍스트 정규화 (비교용)"""
    if not isinstance(text, str):
        text = str(text)
    return re.sub(r"\s+", " ", text.strip()).lower()


def _parse_number(value: Any) -> Optional[float]:
    """문자열/숫자에서 float 파싱"""
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        clean = re.sub(r"[,\s]", "", value.strip())
        match = re.search(r"-?\d+\.?\d*", clean)
        if match:
            try:
                return float(match.group())
            except ValueError:
                pass
    return None


def _similarity(a: str, b: str) -> float:
    """두 문자열 유사도 (0~1)"""
    a_norm = _normalize_text(a)
    b_norm = _normalize_text(b)
    if a_norm == b_norm:
        return 1.0
    return SequenceMatcher(None, a_norm, b_norm).ratio()


def _identify_columns(headers: List[Any]) -> Dict[str, int]:
    """헤더 행에서 컬럼 유형 식별"""
    col_map: Dict[str, int] = {}
    for i, header in enumerate(headers):
        if not header:
            continue
        h_lower = _normalize_text(str(header))
        for col_type, keywords in COLUMN_KEYWORDS.items():
            if col_type in col_map:
                continue
            if any(kw in h_lower for kw in keywords):
                col_map[col_type] = i
                break
    return col_map


def _extract_items(table_data: List[List[Any]]) -> List[Dict[str, Any]]:
    """표 데이터에서 항목 목록 추출"""
    if not table_data or len(table_data) < 2:
        return []

    # 헤더 행 찾기 (처음 3행 중 컬럼 인식률이 높은 행)
    col_map: Dict[str, int] = {}
    data_start = 1
    for i in range(min(3, len(table_data))):
        candidate = _identify_columns(table_data[i])
        if len(candidate) >= len(col_map):
            col_map = candidate
            data_start = i + 1

    if len(col_map) < 2:
        return []

    items = []
    for row in table_data[data_start:]:
        item: Dict[str, Any] = {}
        for col_type, col_idx in col_map.items():
            if col_idx < len(row):
                item[col_type] = row[col_idx]

        name_val = item.get("name") or item.get("code") or ""
        name_str = str(name_val).strip()
        if not name_str:
            continue

        item["_raw_name"] = name_str
        item["_quantity_parsed"] = _parse_number(item.get("quantity"))
        item["_unit"] = str(item.get("unit", "")).strip()
        items.append(item)

    return items


def _match_items(
    items_a: List[Dict], items_b: List[Dict]
) -> List[Tuple[Dict, Dict, float]]:
    """두 문서 항목 목록을 이름/코드 기준으로 매칭"""
    matches = []
    used_b: set = set()

    for a in items_a:
        name_a = a.get("_raw_name", "")
        code_a = str(a.get("code", "")).strip()
        best_idx = -1
        best_score = 0.0

        for j, b in enumerate(items_b):
            if j in used_b:
                continue
            code_b = str(b.get("code", "")).strip()
            # 코드 일치 우선
            if code_a and code_b and code_a == code_b:
                best_idx = j
                best_score = 1.0
                break
            score = _similarity(name_a, b.get("_raw_name", ""))
            if score > best_score:
                best_score = score
                best_idx = j

        if best_idx >= 0 and best_score >= 0.7:
            used_b.add(best_idx)
            matches.append((a, items_b[best_idx], best_score))

    return matches


def _compare_pair(
    doc_a: Dict[str, Any],
    doc_b: Dict[str, Any],
    label_a: str,
    label_b: str,
) -> Tuple[Dict[str, Any], List[Dict[str, Any]]]:
    """두 문서 직접 비교 → (비교 결과, 이슈 목록)"""
    items_a: List[Dict] = []
    for t in doc_a.get("tables", []):
        items_a.extend(_extract_items(t.get("data", [])))

    items_b: List[Dict] = []
    for t in doc_b.get("tables", []):
        items_b.extend(_extract_items(t.get("data", [])))

    if not items_a or not items_b:
        return {
            "consistent": True,
            "items_checked": 0,
            "mismatches": [],
            "note": (
                f"표 구조를 인식할 수 없거나 항목이 없습니다 "
                f"({label_a}: {len(items_a)}개, {label_b}: {len(items_b)}개)"
            ),
        }, []

    matches = _match_items(items_a, items_b)
    pair_mismatches = []
    issues: List[Dict[str, Any]] = []

    for a, b, _ in matches:
        name = a["_raw_name"]
        qty_a = a["_quantity_parsed"]
        qty_b = b["_quantity_parsed"]
        unit_a = a["_unit"]
        unit_b = b["_unit"]

        if qty_a is not None and qty_b is not None:
            denom = max(abs(qty_a), abs(qty_b), 1e-9)
            rel_diff = abs(qty_a - qty_b) / denom
            if rel_diff > 0.01:
                mismatch = {"item": name, "qty_a": qty_a, "qty_b": qty_b, "diff_pct": round(rel_diff * 100, 2)}
                pair_mismatches.append(mismatch)
                issues.append({
                    "level": "error",
                    "category": "수량불일치",
                    "description": f"[{name}] 수량 불일치",
                    "source_doc": label_a,
                    "target_doc": label_b,
                    "detail": f"{label_a}: {qty_a}, {label_b}: {qty_b} (차이: {mismatch['diff_pct']}%)",
                    "recommendation": "두 문서의 해당 항목 수량을 확인하여 수정하세요.",
                })

        if unit_a and unit_b and _normalize_text(unit_a) != _normalize_text(unit_b):
            pair_mismatches.append({"item": name, "unit_a": unit_a, "unit_b": unit_b})
            issues.append({
                "level": "warning",
                "category": "단위불일치",
                "description": f"[{name}] 단위 불일치",
                "source_doc": label_a,
                "target_doc": label_b,
                "detail": f"{label_a}: {unit_a}, {label_b}: {unit_b}",
                "recommendation": "단위를 통일하거나 환산 여부를 확인하세요.",
            })

    matched_names_a = {a["_raw_name"] for a, _, _ in matches}
    matched_names_b = {b["_raw_name"] for _, b, _ in matches}
    unmatched_a = [i["_raw_name"] for i in items_a if i["_raw_name"] not in matched_names_a]
    unmatched_b = [i["_raw_name"] for i in items_b if i["_raw_name"] not in matched_names_b]

    for item_name in unmatched_a[:20]:
        issues.append({
            "level": "warning",
            "category": "누락항목",
            "description": f"[{item_name}] {label_b}에 없는 항목",
            "source_doc": label_a,
            "target_doc": label_b,
            "detail": f"'{item_name}' 항목이 {label_b}에서 발견되지 않았습니다.",
            "recommendation": f"{label_b}에 항목을 추가하거나 항목명을 확인하세요.",
        })

    comparison = {
        "consistent": len(pair_mismatches) == 0 and len(unmatched_a) == 0,
        "items_checked": len(matches),
        "mismatches": pair_mismatches,
        "unmatched_in_b": unmatched_b[:20],
    }
    return comparison, issues


def compare_documents(
    documents: List[Dict[str, Any]],
    analysis_types: List[str],
) -> Dict[str, Any]:
    """업로드된 발주도서 직접 비교 분석 (AI 없이)"""

    drawing = next((d for d in documents if d["doc_type"] == "drawing"), None)
    quantity = next((d for d in documents if d["doc_type"] == "quantity"), None)
    boq = next((d for d in documents if d["doc_type"] == "boq"), None)

    all_issues: List[Dict[str, Any]] = []
    cross_comparison: Dict[str, Any] = {}

    if "cross_check" in analysis_types:
        if quantity and boq:
            result, issues = _compare_pair(
                quantity["content"], boq["content"],
                quantity["filename"], boq["filename"],
            )
            cross_comparison["quantity_vs_boq"] = result
            all_issues.extend(issues)

        if drawing and quantity:
            result, issues = _compare_pair(
                drawing["content"], quantity["content"],
                drawing["filename"], quantity["filename"],
            )
            cross_comparison["drawing_vs_quantity"] = result
            all_issues.extend(issues)

    if "quantity_check" in analysis_types:
        for doc in filter(None, [quantity, boq]):
            for table in doc["content"].get("tables", []):
                for item in _extract_items(table.get("data", [])):
                    qty = item["_quantity_parsed"]
                    if qty is not None and qty < 0:
                        all_issues.append({
                            "level": "error",
                            "category": "계산오류",
                            "description": f"[{item['_raw_name']}] 음수 수량 발견",
                            "source_doc": doc["filename"],
                            "detail": f"수량 값이 음수({qty})입니다.",
                            "recommendation": "수량 계산식을 확인하세요.",
                        })

    errors = sum(1 for i in all_issues if i["level"] == "error")
    warnings = sum(1 for i in all_issues if i["level"] == "warning")
    infos = sum(1 for i in all_issues if i["level"] == "info")
    doc_names = [d["filename"] for d in documents]

    if not all_issues:
        summary = f"검토 완료 ({', '.join(doc_names)}) — 오류나 불일치가 발견되지 않았습니다."
    else:
        summary = (
            f"검토 완료 ({', '.join(doc_names)}) — "
            f"총 {len(all_issues)}건 발견 "
            f"(오류: {errors}건, 경고: {warnings}건)"
        )

    return {
        "summary": summary,
        "statistics": {
            "total_issues": len(all_issues),
            "errors": errors,
            "warnings": warnings,
            "infos": infos,
            "documents_reviewed": len(documents),
        },
        "issues": all_issues,
        "cross_comparison": cross_comparison or None,
        "standard_check": None,
        "raw_analysis": None,
    }
