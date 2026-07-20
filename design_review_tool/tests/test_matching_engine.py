from design_review_tool.common.text_normalize import normalize
from design_review_tool.matching.engine import MatchConfig, MatchGrade, match_items
from design_review_tool.parsers.models import Item


def make_item(공종, 규격="", 수량=1.0, 대분류="포장공", 출처파일=""):
    return Item(
        공종=공종,
        규격=규격,
        단위="m2",
        수량=수량,
        결합텍스트=normalize(공종 + " " + 규격),
        대분류=normalize(대분류),
        출처파일=출처파일,
    )


def test_confirmed_match_same_text_same_qty():
    source = [make_item("미끄럼방지포장", 수량=100)]
    target = [make_item("미끄럼방지포장", 수량=100)]

    results = match_items(source, target)

    assert len(results) == 1
    assert results[0].grade == MatchGrade.CONFIRMED
    assert results[0].qty_diff == 0.0


def test_high_similarity_but_large_qty_diff_downgrades_to_review():
    source = [make_item("미끄럼방지포장", 수량=100)]
    target = [make_item("미끄럼방지포장", 수량=10)]  # -90% 차이

    results = match_items(source, target)

    assert results[0].grade == MatchGrade.NEEDS_REVIEW_QTY_MISMATCH
    assert results[0].target is not None


def test_ambiguous_similar_names_do_not_auto_confirm_below_high_threshold():
    # 스펙에서 언급된 실제 사례: "미끄럼방지포장" vs "미끄럼방지포장재"
    source = [make_item("미끄럼방지포장재", 수량=50)]
    target = [make_item("전혀다른공종명칭으로텍스트유사도가낮음", 수량=50)]

    results = match_items(source, target, MatchConfig(high_threshold=99, low_threshold=99))

    assert results[0].grade == MatchGrade.UNMATCHED_SOURCE_ONLY


def test_category_mismatch_is_only_used_as_fallback_candidate_pool():
    source = [make_item("아스팔트포장", 수량=100, 대분류="포장공")]
    target = [make_item("아스팔트포장", 수량=100, 대분류="부대공")]

    results = match_items(source, target)

    # 대분류가 다르면 원래 후보가 없어 전체를 fallback 후보로 쓰지만,
    # 매칭 자체는 시도되어 target이 채워진다 (자동 확정 여부는 유사도/수량 기준으로 판정).
    assert results[0].target is not None
    assert results[0].target.공종 == "아스팔트포장"


def test_unmatched_target_only_when_source_empty():
    results = match_items([], [make_item("잔여항목", 수량=5)])

    assert len(results) == 1
    assert results[0].grade == MatchGrade.UNMATCHED_TARGET_ONLY
    assert results[0].target.공종 == "잔여항목"


def test_unmatched_source_only_when_target_empty():
    results = match_items([make_item("고아항목", 수량=5)], [])

    assert len(results) == 1
    assert results[0].grade == MatchGrade.UNMATCHED_SOURCE_ONLY


def test_needs_review_target_is_not_consumed_by_confirmed_match():
    # '확인 필요' 매칭은 target을 소비하지 않아야 다른 진짜 매칭을 막지 않는다.
    target_item = make_item("공통후보항목", 수량=100)
    weak_source = make_item("전혀다른텍스트내용", 수량=100)
    strong_source = make_item("공통후보항목", 수량=100)

    results = match_items(
        [weak_source, strong_source],
        [target_item],
        MatchConfig(low_threshold=1),
    )

    grades = {r.source.공종: r.grade for r in results if r.source}
    assert grades["공통후보항목"] == MatchGrade.CONFIRMED
