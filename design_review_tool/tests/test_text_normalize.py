from design_review_tool.common.text_normalize import normalize


def test_removes_whitespace_and_punctuation():
    assert normalize("아스팔트 절삭 후, 덧씌우기") == "아스팔트절삭후덧씌우기"


def test_word_order_survives_only_via_scorer_not_normalize():
    # normalize 자체는 어순을 바꾸지 않는다 (어순 변형 대응은 token_sort_ratio 몫).
    assert normalize("절삭후아스팔트덧씌우기") != normalize("아스팔트 절삭 후 덧씌우기")


def test_lowercases_and_strips_parentheses():
    assert normalize("ABC(test), value") == "abctestvalue"
