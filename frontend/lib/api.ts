const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8000";

export type DocumentType = "drawing" | "quantity" | "boq";

export interface UploadedDocument {
  id: string;
  filename: string;
  doc_type: DocumentType;
  status: "pending" | "processing" | "done" | "error";
  error?: string;
  parsed_data?: Record<string, unknown>;
}

export interface Issue {
  level: "error" | "warning" | "info";
  category: string;
  description: string;
  source_doc?: string;
  target_doc?: string;
  detail?: string;
  recommendation?: string;
}

export interface AnalysisResult {
  session_id: string;
  summary: string;
  issues: Issue[];
  statistics?: {
    total_issues: number;
    errors: number;
    warnings: number;
    infos: number;
    documents_reviewed: number;
  };
  cross_comparison?: {
    drawing_vs_quantity?: { consistent: boolean; items_checked: number; mismatches: unknown[] };
    quantity_vs_boq?: { consistent: boolean; items_checked: number; mismatches: unknown[] };
  };
  standard_check?: {
    compliant: boolean;
    violations: unknown[];
    notes: string[];
  };
  raw_analysis?: string;
}

export async function uploadDocument(
  file: File,
  docType: DocumentType
): Promise<UploadedDocument> {
  const form = new FormData();
  form.append("file", file);
  form.append("doc_type", docType);

  const res = await fetch(`${API_BASE}/api/documents/upload`, {
    method: "POST",
    body: form,
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({ detail: "업로드 실패" }));
    throw new Error(err.detail || "업로드 실패");
  }
  return res.json();
}

export async function listDocuments(): Promise<UploadedDocument[]> {
  const res = await fetch(`${API_BASE}/api/documents/`);
  if (!res.ok) throw new Error("목록 조회 실패");
  return res.json();
}

export async function deleteDocument(docId: string): Promise<void> {
  await fetch(`${API_BASE}/api/documents/${docId}`, { method: "DELETE" });
}

export async function clearAllDocuments(): Promise<void> {
  await fetch(`${API_BASE}/api/documents/`, { method: "DELETE" });
}

export async function runAnalysis(
  documentIds: string[],
  analysisTypes: string[]
): Promise<AnalysisResult> {
  const res = await fetch(`${API_BASE}/api/analysis/run`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ document_ids: documentIds, analysis_types: analysisTypes }),
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({ detail: "분석 실패" }));
    throw new Error(err.detail || "분석 실패");
  }
  return res.json();
}

export function getStreamAnalysisUrl(documentIds: string[], analysisTypes: string[]): string {
  const ids = documentIds.join(",");
  const types = analysisTypes.join(",");
  return `${API_BASE}/api/analysis/stream?document_ids=${encodeURIComponent(ids)}&analysis_types=${encodeURIComponent(types)}`;
}
