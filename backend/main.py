"""
건설공사 발주도서 검토 툴 - FastAPI 백엔드
"""
import logging
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pathlib import Path

from routers import documents, analysis

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="건설공사 발주도서 검토 툴",
    description="설계도면, 수량산출서, 내역서 상호 비교 및 표준품셈 검토 시스템",
    version="1.0.0",
)

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://localhost:3001"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 라우터 등록
app.include_router(documents.router)
app.include_router(analysis.router)

# 업로드 디렉토리 생성
Path("uploads").mkdir(exist_ok=True)


@app.get("/api/health")
async def health_check():
    return {
        "status": "ok",
        "service": "건설공사 발주도서 검토 툴",
        "version": "1.0.0",
    }


@app.get("/api/supported-formats")
async def supported_formats():
    return {
        "drawing": {
            "label": "설계도면",
            "formats": [".pdf", ".dxf", ".dwg"],
            "description": "설계도면 파일 (PDF, AutoCAD DXF/DWG)",
        },
        "quantity": {
            "label": "수량산출서",
            "formats": [".xlsx", ".xls", ".csv", ".pdf"],
            "description": "수량산출서 파일 (Excel, CSV, PDF)",
        },
        "boq": {
            "label": "내역서",
            "formats": [".xlsx", ".xls", ".csv", ".pdf"],
            "description": "내역서/공사비내역서 파일 (Excel, CSV, PDF)",
        },
    }
