from __future__ import annotations

from dataclasses import dataclass


@dataclass
class Item:
    """SGS내역서 또는 수량산출서에서 정규화된 한 행."""

    공종: str
    규격: str
    단위: str
    수량: float
    결합텍스트: str
    대분류: str
    출처파일: str = ""
    출처행: int = -1
