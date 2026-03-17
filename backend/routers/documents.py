"""
문서 업로드 및 관리 라우터
"""
import os
import uuid
import logging
from pathlib import Path
from typing import List

from fastapi import APIRouter, UploadFile, File, Form, HTTPException
import aiofiles

from models.schemas import DocumentType, DocumentStatus, UploadedDocument
from services.document_parser import parse_document, truncate_for_context

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/documents", tags=["documents"])

# 인메모리 저장소 (실제 서비스는 DB 사용)
_documents: dict[str, UploadedDocument] = {}
UPLOAD_DIR = Path("uploads")
UPLOAD_DIR.mkdir(exist_ok=True)

ALLOWED_EXTENSIONS = {".pdf", ".xlsx", ".xls", ".csv", ".dxf", ".dwg"}
MAX_FILE_SIZE = 50 * 1024 * 1024  # 50MB


@router.post("/upload", response_model=UploadedDocument)
async def upload_document(
    file: UploadFile = File(...),
    doc_type: DocumentType = Form(...),
):
    """발주도서 파일 업로드"""
    # 파일 확장자 검사
    ext = Path(file.filename).suffix.lower()
    if ext not in ALLOWED_EXTENSIONS:
        raise HTTPException(
            status_code=400,
            detail=f"지원하지 않는 파일 형식입니다. 지원 형식: {', '.join(ALLOWED_EXTENSIONS)}",
        )

    doc_id = str(uuid.uuid4())
    file_path = UPLOAD_DIR / f"{doc_id}{ext}"

    # 파일 저장
    content = await file.read()
    if len(content) > MAX_FILE_SIZE:
        raise HTTPException(status_code=413, detail="파일 크기가 50MB를 초과합니다.")

    async with aiofiles.open(file_path, "wb") as f:
        await f.write(content)

    doc = UploadedDocument(
        id=doc_id,
        filename=file.filename,
        doc_type=doc_type,
        status=DocumentStatus.PROCESSING,
        file_path=str(file_path),
    )
    _documents[doc_id] = doc

    # 파싱 실행
    try:
        parsed = parse_document(str(file_path), doc_type.value, file.filename)
        doc.parsed_data = parsed
        doc.status = DocumentStatus.DONE
    except Exception as e:
        logger.error(f"파싱 오류: {e}")
        doc.status = DocumentStatus.ERROR
        doc.error = str(e)

    return doc


@router.get("/", response_model=List[UploadedDocument])
async def list_documents():
    """업로드된 문서 목록"""
    return list(_documents.values())


@router.get("/{doc_id}", response_model=UploadedDocument)
async def get_document(doc_id: str):
    """특정 문서 조회"""
    doc = _documents.get(doc_id)
    if not doc:
        raise HTTPException(status_code=404, detail="문서를 찾을 수 없습니다.")
    return doc


@router.delete("/{doc_id}")
async def delete_document(doc_id: str):
    """문서 삭제"""
    doc = _documents.pop(doc_id, None)
    if not doc:
        raise HTTPException(status_code=404, detail="문서를 찾을 수 없습니다.")
    if doc.file_path and Path(doc.file_path).exists():
        os.remove(doc.file_path)
    return {"message": "삭제되었습니다."}


@router.delete("/")
async def clear_all_documents():
    """모든 문서 초기화"""
    for doc in _documents.values():
        if doc.file_path and Path(doc.file_path).exists():
            try:
                os.remove(doc.file_path)
            except Exception:
                pass
    _documents.clear()
    return {"message": "초기화되었습니다."}


def get_document_content(doc_id: str) -> dict:
    """분석용 문서 내용 반환"""
    doc = _documents.get(doc_id)
    if not doc or not doc.parsed_data:
        return None
    return {
        "id": doc.id,
        "doc_type": doc.doc_type.value,
        "filename": doc.filename,
        "content": truncate_for_context(doc.parsed_data),
        "parsed_data": doc.parsed_data,
    }
