"""
발주도서 파싱 서비스
- PDF: pdfplumber (텍스트/표 추출)
- Excel/CSV: pandas/openpyxl
- CAD DXF: ezdxf
"""
import os
import json
import base64
import logging
from pathlib import Path
from typing import Dict, Any, List, Optional

import pdfplumber
import pandas as pd
import ezdxf
from ezdxf import recover

logger = logging.getLogger(__name__)


def parse_document(file_path: str, doc_type: str, filename: str) -> Dict[str, Any]:
    """파일 확장자에 따라 적절한 파서 호출"""
    ext = Path(filename).suffix.lower()

    try:
        if ext == ".pdf":
            return parse_pdf(file_path, doc_type)
        elif ext in (".xlsx", ".xls"):
            return parse_excel(file_path, doc_type)
        elif ext == ".csv":
            return parse_csv(file_path, doc_type)
        elif ext == ".dxf":
            return parse_dxf(file_path, doc_type)
        elif ext == ".dwg":
            return parse_dwg_fallback(file_path, doc_type)
        else:
            return {"error": f"지원하지 않는 파일 형식: {ext}", "text": "", "tables": []}
    except Exception as e:
        logger.error(f"문서 파싱 오류 ({filename}): {e}")
        return {"error": str(e), "text": "", "tables": []}


def parse_pdf(file_path: str, doc_type: str) -> Dict[str, Any]:
    """PDF 파싱 - 텍스트와 표 추출"""
    result = {
        "type": "pdf",
        "doc_type": doc_type,
        "pages": [],
        "tables": [],
        "text": "",
        "metadata": {},
    }

    with pdfplumber.open(file_path) as pdf:
        result["metadata"]["total_pages"] = len(pdf.pages)
        all_text = []

        for i, page in enumerate(pdf.pages):
            page_data = {"page_num": i + 1, "text": "", "tables": []}

            # 텍스트 추출
            text = page.extract_text() or ""
            page_data["text"] = text
            all_text.append(text)

            # 표 추출
            tables = page.extract_tables()
            for table in tables:
                if table:
                    cleaned = [[cell or "" for cell in row] for row in table]
                    page_data["tables"].append(cleaned)
                    result["tables"].append({"page": i + 1, "data": cleaned})

            result["pages"].append(page_data)

        result["text"] = "\n".join(all_text)

    return result


def parse_excel(file_path: str, doc_type: str) -> Dict[str, Any]:
    """Excel 파싱 - 모든 시트 추출"""
    result = {
        "type": "excel",
        "doc_type": doc_type,
        "sheets": {},
        "tables": [],
        "text": "",
    }

    xl = pd.ExcelFile(file_path)
    all_text_parts = []

    for sheet_name in xl.sheet_names:
        df = pd.read_excel(file_path, sheet_name=sheet_name, header=None)
        # NaN을 빈 문자열로
        df = df.fillna("")
        rows = df.values.tolist()
        # 완전히 빈 행 제거
        rows = [r for r in rows if any(str(c).strip() for c in r)]

        result["sheets"][sheet_name] = rows
        result["tables"].append({"sheet": sheet_name, "data": rows})

        # 텍스트 표현 생성
        text_lines = [f"[시트: {sheet_name}]"]
        for row in rows:
            line = " | ".join(str(c) for c in row if str(c).strip())
            if line:
                text_lines.append(line)
        all_text_parts.append("\n".join(text_lines))

    result["text"] = "\n\n".join(all_text_parts)
    return result


def parse_csv(file_path: str, doc_type: str) -> Dict[str, Any]:
    """CSV 파싱"""
    result = {"type": "csv", "doc_type": doc_type, "tables": [], "text": ""}

    # 한국어 CSV는 cp949 또는 utf-8-sig 인코딩
    for encoding in ("utf-8-sig", "cp949", "utf-8"):
        try:
            df = pd.read_csv(file_path, encoding=encoding, header=None)
            break
        except (UnicodeDecodeError, Exception):
            continue

    df = df.fillna("")
    rows = df.values.tolist()
    rows = [r for r in rows if any(str(c).strip() for c in r)]

    result["tables"].append({"sheet": "CSV", "data": rows})
    lines = [" | ".join(str(c) for c in row if str(c).strip()) for row in rows]
    result["text"] = "\n".join(lines)
    return result


def parse_dxf(file_path: str, doc_type: str) -> Dict[str, Any]:
    """DXF CAD 파일 파싱 - 레이어, 텍스트, 치수 추출"""
    result = {
        "type": "dxf",
        "doc_type": doc_type,
        "layers": [],
        "texts": [],
        "dimensions": [],
        "blocks": [],
        "text": "",
        "metadata": {},
    }

    try:
        doc, auditor = recover.readfile(file_path)
    except Exception:
        doc = ezdxf.readfile(file_path)

    msp = doc.modelspace()

    # 레이어 목록
    result["layers"] = [layer.dxf.name for layer in doc.layers]
    result["metadata"]["layer_count"] = len(result["layers"])

    text_parts = [f"도면 레이어: {', '.join(result['layers'])}"]

    # 엔티티 순회
    for entity in msp:
        etype = entity.dxftype()

        if etype in ("TEXT", "MTEXT"):
            try:
                text = entity.dxf.text if etype == "TEXT" else entity.text
                layer = entity.dxf.layer
                result["texts"].append({"type": etype, "layer": layer, "text": text})
                text_parts.append(f"텍스트[{layer}]: {text}")
            except Exception:
                pass

        elif etype == "DIMENSION":
            try:
                dim_text = entity.dxf.text or ""
                measurement = getattr(entity.dxf, "actual_measurement", None)
                result["dimensions"].append(
                    {
                        "layer": entity.dxf.layer,
                        "text": dim_text,
                        "measurement": measurement,
                    }
                )
            except Exception:
                pass

        elif etype == "INSERT":
            try:
                result["blocks"].append(
                    {"name": entity.dxf.name, "layer": entity.dxf.layer}
                )
            except Exception:
                pass

    result["metadata"]["text_count"] = len(result["texts"])
    result["metadata"]["dimension_count"] = len(result["dimensions"])
    result["metadata"]["block_count"] = len(result["blocks"])
    result["text"] = "\n".join(text_parts)

    return result


def parse_dwg_fallback(file_path: str, doc_type: str) -> Dict[str, Any]:
    """DWG 파일 - ezdxf는 일부 DWG를 지원, 실패시 안내 메시지"""
    try:
        # ezdxf가 일부 DWG 읽기 지원
        doc = ezdxf.readfile(file_path)
        result = parse_dxf.__wrapped__(file_path, doc_type) if hasattr(parse_dxf, '__wrapped__') else parse_dxf(file_path, doc_type)
        result["type"] = "dwg"
        return result
    except Exception as e:
        return {
            "type": "dwg",
            "doc_type": doc_type,
            "text": "DWG 파일 직접 읽기 실패. DXF로 변환 후 업로드를 권장합니다.",
            "tables": [],
            "layers": [],
            "error": str(e),
            "warning": "DWG 파일은 AutoCAD에서 DXF로 변환(다른 이름으로 저장 → DXF) 후 업로드하면 더 정확한 분석이 가능합니다.",
        }


def truncate_for_context(parsed_data: Dict[str, Any], max_chars: int = 15000) -> str:
    """Claude 컨텍스트용 텍스트 생성 (과도한 길이 방지)"""
    text = parsed_data.get("text", "")
    if len(text) > max_chars:
        text = text[:max_chars] + f"\n... (이하 {len(text)-max_chars}자 생략)"

    # 표 데이터 요약
    tables = parsed_data.get("tables", [])
    table_summaries = []
    for i, t in enumerate(tables[:5]):  # 최대 5개 표
        sheet = t.get("sheet", t.get("page", i + 1))
        data = t.get("data", [])
        if data:
            header = " | ".join(str(c) for c in data[0])
            row_count = len(data) - 1
            table_summaries.append(f"표[{sheet}] 헤더: {header} (총 {row_count}행)")

    if table_summaries:
        text += "\n\n[표 구조]\n" + "\n".join(table_summaries)

    return text
