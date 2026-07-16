from design_review_tool.common.values import is_number


def test_numbers_are_numbers():
    assert is_number(1.5)
    assert is_number(3)


def test_strings_and_none_and_bool_are_not_numbers():
    assert not is_number("3")
    assert not is_number(None)
    assert not is_number(True)
    assert not is_number("")
