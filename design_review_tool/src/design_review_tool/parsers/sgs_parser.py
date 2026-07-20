from __future__ import annotations

import re
from typing import Any, Sequence

from design_review_tool.common.text_normalize import normalize
from design_review_tool.common.values import is_number
from design_review_tool.io.excel_reader import SheetReader
from design_review_tool.parsers.models import Item

_CATEGORY_ROW_RE = re.compile(r"^\d+\.")


def _cell(row: Sequence[Any], idx: int) -> Any:
    return row[idx] if idx < len(row) else ""


def parse_sgs(path: str, sheet_name: str = "내역서") -> list[Item]:
    """
    SGS내역서를 파싱한다.
    열 구조(고정): [0]공종 [1]규격 [2]수량 [3]단위 [4]단가 [5]금액 ...

    - 대분류 행(예: "1. 포 장 공"): 공종은 있고 단위/규격은 없는 행 중
      숫자+점으로 시작하는 행을 상위 카테고리로 취급한다.
    - 데이터 행: 공종이 있고, 수량이 숫자이며, 단위가 있는 행.
    """
    reader = SheetReader(path)
    items: list[Item] = []
    current_category = ""

    for r, row in enumerate(reader.rows(sheet_name)):
        gongjong = _cell(row, 0)
        gyugyeok = _cell(row, 1)
        suryang = _cell(row, 2)
        danwi = _cell(row, 3)

        if gongjong and not danwi and not gyugyeok:
            text = str(gongjong).strip()
            if _CATEGORY_ROW_RE.match(text):
                current_category = normalize(text)
                continue

        if gongjong and is_number(suryang) and danwi:
            name = str(gongjong).strip()
            spec = str(gyugyeok).strip() if gyugyeok else ""
            items.append(Item(
                공종=name,
                규격=spec,
                단위=str(danwi).strip(),
                수량=float(suryang),
                결합텍스트=normalize(name + " " + spec),
                대분류=current_category,
                출처파일=path.rsplit("/", 1)[-1],
                출처행=r,
            ))
    return items
