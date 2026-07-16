from typing import Any


def is_number(value: Any) -> bool:
    """xlrd/openpyxl이 돌려주는 셀 값 중 실제 숫자(bool 제외)인지 판별한다."""
    return isinstance(value, (int, float)) and not isinstance(value, bool)
