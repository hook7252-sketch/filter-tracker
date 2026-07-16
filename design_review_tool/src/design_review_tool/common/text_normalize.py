import re

_WHITESPACE_RE = re.compile(r"\s+")
_PUNCTUATION_RE = re.compile(r"[(),./]")


def normalize(text: str) -> str:
    """매칭 비교용 텍스트: 공백/괄호/쉼표 등을 제거하고 소문자화한다."""
    text = _WHITESPACE_RE.sub("", text)
    text = _PUNCTUATION_RE.sub("", text)
    return text.lower()
