"""PDF 설계도서에서 페이지별 텍스트/이미지를 추출한다.

텍스트가 거의 없는 페이지(도면, 스캔 이미지 등)는 비전 분석을 위해
페이지 전체를 PNG로 렌더링해 함께 반환한다.
"""
from __future__ import annotations

import base64
import io
from pathlib import Path

from . import config
from .schemas import PageContent

try:
    import fitz  # PyMuPDF
except ImportError as exc:  # pragma: no cover
    raise ImportError(
        "PyMuPDF가 설치되어 있지 않습니다. `pip install PyMuPDF`로 설치하세요."
    ) from exc


def render_page_png_b64(page: "fitz.Page") -> str:
    """페이지를 PNG로 렌더링해 base64 문자열로 반환한다 (크기 상한 적용)."""
    zoom = config.RENDER_DPI / 72.0
    rect = page.rect
    long_side_px = max(rect.width, rect.height) * zoom
    if long_side_px > config.MAX_IMAGE_LONG_SIDE:
        zoom *= config.MAX_IMAGE_LONG_SIDE / long_side_px
    mat = fitz.Matrix(zoom, zoom)
    pix = page.get_pixmap(matrix=mat, alpha=False)
    return base64.b64encode(pix.tobytes("png")).decode("ascii")


def extract_pages(pdf_path: str | Path) -> list[PageContent]:
    """PDF의 모든 페이지에서 텍스트를 추출하고, 텍스트가 부족한 페이지는 이미지를 함께 담는다."""
    pdf_path = Path(pdf_path)
    if not pdf_path.exists():
        raise FileNotFoundError(f"PDF 파일을 찾을 수 없습니다: {pdf_path}")

    doc = fitz.open(pdf_path)
    pages: list[PageContent] = []
    try:
        for i, page in enumerate(doc, start=1):
            text = page.get_text("text").strip()
            image_b64 = None
            if len(text) < config.IMAGE_FALLBACK_TEXT_THRESHOLD:
                image_b64 = render_page_png_b64(page)
            pages.append(PageContent(page=i, text=text, image_b64=image_b64))
    finally:
        doc.close()
    return pages


def page_count(pdf_path: str | Path) -> int:
    doc = fitz.open(Path(pdf_path))
    try:
        return doc.page_count
    finally:
        doc.close()
