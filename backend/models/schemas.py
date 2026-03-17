from pydantic import BaseModel
from typing import Optional, List, Dict, Any
from enum import Enum


class DocumentType(str, Enum):
    DRAWING = "drawing"          # 설계도면
    QUANTITY = "quantity"        # 수량산출서
    BOQ = "boq"                  # 내역서 (Bill of Quantities)


class DocumentStatus(str, Enum):
    PENDING = "pending"
    PROCESSING = "processing"
    DONE = "done"
    ERROR = "error"


class UploadedDocument(BaseModel):
    id: str
    filename: str
    doc_type: DocumentType
    status: DocumentStatus = DocumentStatus.PENDING
    file_path: Optional[str] = None
    parsed_data: Optional[Dict[str, Any]] = None
    error: Optional[str] = None


class AnalysisRequest(BaseModel):
    document_ids: List[str]
    analysis_types: List[str] = ["cross_check", "standard_check", "quantity_check"]


class IssueLevel(str, Enum):
    ERROR = "error"
    WARNING = "warning"
    INFO = "info"


class Issue(BaseModel):
    level: IssueLevel
    category: str
    description: str
    source_doc: Optional[str] = None
    target_doc: Optional[str] = None
    detail: Optional[str] = None
    recommendation: Optional[str] = None


class AnalysisResult(BaseModel):
    session_id: str
    summary: str
    issues: List[Issue]
    cross_comparison: Optional[Dict[str, Any]] = None
    standard_check: Optional[Dict[str, Any]] = None
    statistics: Optional[Dict[str, Any]] = None
    raw_analysis: Optional[str] = None
