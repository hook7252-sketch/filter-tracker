"use client";

import { useState, useCallback } from "react";
import {
  Building2, Play, Trash2, RefreshCw, ChevronDown, ChevronUp, FileCheck
} from "lucide-react";
import DocumentUploadCard from "@/components/DocumentUploadCard";
import AnalysisResults from "@/components/AnalysisResults";
import {
  uploadDocument, deleteDocument, clearAllDocuments, runAnalysis,
  type DocumentType, type UploadedDocument, type AnalysisResult,
} from "@/lib/api";

const DOC_CONFIGS = [
  {
    docType: "drawing" as DocumentType,
    label: "설계도면",
    description: "건축/토목/기계/전기 설계도면",
    acceptedFormats: "PDF, DXF, DWG",
    color: "blue",
  },
  {
    docType: "quantity" as DocumentType,
    label: "수량산출서",
    description: "공종별 수량 산출 내역",
    acceptedFormats: "Excel, CSV, PDF",
    color: "green",
  },
  {
    docType: "boq" as DocumentType,
    label: "내역서",
    description: "공사비 내역서 (Bill of Quantities)",
    acceptedFormats: "Excel, CSV, PDF",
    color: "orange",
  },
];

const ANALYSIS_OPTIONS = [
  { id: "cross_check", label: "문서 간 상호 비교", desc: "도면↔수량산출서↔내역서 정합성 검토" },
  { id: "standard_check", label: "표준품셈 기준 검토", desc: "건설공사 표준품셈 및 원가기준 비교" },
  { id: "quantity_check", label: "수량 산출 검토", desc: "수량 계산식 및 단위 오류 검토" },
];

export default function HomePage() {
  const [documents, setDocuments] = useState<Record<DocumentType, UploadedDocument | undefined>>({
    drawing: undefined,
    quantity: undefined,
    boq: undefined,
  });
  const [analysisTypes, setAnalysisTypes] = useState<string[]>([
    "cross_check", "standard_check", "quantity_check",
  ]);
  const [analyzing, setAnalyzing] = useState(false);
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showOptions, setShowOptions] = useState(false);

  const handleUpload = useCallback(async (file: File, docType: DocumentType) => {
    setError(null);
    try {
      const doc = await uploadDocument(file, docType);
      setDocuments((prev) => ({ ...prev, [docType]: doc }));
    } catch (e) {
      setError(e instanceof Error ? e.message : "업로드 실패");
    }
  }, []);

  const handleDelete = useCallback(async (docId: string) => {
    await deleteDocument(docId);
    setDocuments((prev) => {
      const next = { ...prev };
      for (const key of Object.keys(next) as DocumentType[]) {
        if (next[key]?.id === docId) next[key] = undefined;
      }
      return next;
    });
  }, []);

  const handleClearAll = async () => {
    await clearAllDocuments();
    setDocuments({ drawing: undefined, quantity: undefined, boq: undefined });
    setResult(null);
    setError(null);
  };

  const readyDocs = Object.values(documents).filter(
    (d) => d && d.status === "done"
  ) as UploadedDocument[];

  const handleAnalyze = async () => {
    if (readyDocs.length < 2) {
      setError("분석을 위해 최소 2개 이상의 문서를 업로드하세요.");
      return;
    }
    if (analysisTypes.length === 0) {
      setError("검토 항목을 하나 이상 선택하세요.");
      return;
    }
    setAnalyzing(true);
    setError(null);
    setResult(null);
    try {
      const r = await runAnalysis(
        readyDocs.map((d) => d.id),
        analysisTypes
      );
      setResult(r);
      setTimeout(() => {
        document.getElementById("results")?.scrollIntoView({ behavior: "smooth" });
      }, 100);
    } catch (e) {
      setError(e instanceof Error ? e.message : "분석 실패");
    } finally {
      setAnalyzing(false);
    }
  };

  const toggleAnalysisType = (type: string) => {
    setAnalysisTypes((prev) =>
      prev.includes(type) ? prev.filter((t) => t !== type) : [...prev, type]
    );
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-5xl mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
              <Building2 className="w-5 h-5 text-white" />
            </div>
            <div>
              <h1 className="font-bold text-gray-900 text-sm sm:text-base leading-tight">
                건설공사 발주도서 검토 툴
              </h1>
              <p className="text-xs text-gray-500 hidden sm:block">
                AI 기반 설계도면 · 수량산출서 · 내역서 상호 검토
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            {Object.values(documents).some(Boolean) && (
              <button
                onClick={handleClearAll}
                className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-red-500 px-2.5 py-1.5 rounded-md hover:bg-red-50 transition-colors"
              >
                <Trash2 className="w-3.5 h-3.5" />
                초기화
              </button>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-5xl mx-auto px-4 py-6 space-y-6">
        {/* Step 1: 문서 업로드 */}
        <section>
          <div className="flex items-center gap-2 mb-4">
            <span className="w-6 h-6 rounded-full bg-blue-600 text-white text-xs font-bold flex items-center justify-center flex-shrink-0">
              1
            </span>
            <h2 className="font-semibold text-gray-800">발주도서 업로드</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {DOC_CONFIGS.map((cfg) => (
              <DocumentUploadCard
                key={cfg.docType}
                {...cfg}
                document={documents[cfg.docType]}
                onUpload={handleUpload}
                onDelete={handleDelete}
              />
            ))}
          </div>
        </section>

        {/* Step 2: 검토 옵션 */}
        <section>
          <button
            onClick={() => setShowOptions(!showOptions)}
            className="flex items-center gap-2 mb-3 text-gray-700 hover:text-gray-900"
          >
            <span className="w-6 h-6 rounded-full bg-gray-200 text-gray-600 text-xs font-bold flex items-center justify-center flex-shrink-0">
              2
            </span>
            <span className="font-semibold">검토 항목 선택</span>
            {showOptions ? (
              <ChevronUp className="w-4 h-4 text-gray-400" />
            ) : (
              <ChevronDown className="w-4 h-4 text-gray-400" />
            )}
          </button>
          {showOptions && (
            <div className="bg-white rounded-xl border border-gray-200 p-4">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                {ANALYSIS_OPTIONS.map((opt) => (
                  <label
                    key={opt.id}
                    className={`flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors ${
                      analysisTypes.includes(opt.id)
                        ? "border-blue-400 bg-blue-50"
                        : "border-gray-200 hover:border-gray-300"
                    }`}
                  >
                    <input
                      type="checkbox"
                      checked={analysisTypes.includes(opt.id)}
                      onChange={() => toggleAnalysisType(opt.id)}
                      className="mt-0.5"
                    />
                    <div>
                      <p className="text-sm font-medium text-gray-700">{opt.label}</p>
                      <p className="text-xs text-gray-500 mt-0.5">{opt.desc}</p>
                    </div>
                  </label>
                ))}
              </div>
            </div>
          )}
        </section>

        {/* Step 3: 분석 실행 */}
        <section>
          <div className="flex items-center gap-2 mb-3">
            <span className="w-6 h-6 rounded-full bg-gray-200 text-gray-600 text-xs font-bold flex items-center justify-center flex-shrink-0">
              3
            </span>
            <h2 className="font-semibold text-gray-800">AI 검토 실행</h2>
          </div>

          <div className="flex flex-wrap gap-2 mb-3">
            {DOC_CONFIGS.map((cfg) => (
              <span
                key={cfg.docType}
                className={`text-xs px-2.5 py-1 rounded-full flex items-center gap-1 ${
                  documents[cfg.docType]?.status === "done"
                    ? "bg-green-100 text-green-700"
                    : "bg-gray-100 text-gray-500"
                }`}
              >
                {documents[cfg.docType]?.status === "done" ? (
                  <FileCheck className="w-3 h-3" />
                ) : (
                  <span className="w-3 h-3 rounded-full border border-current" />
                )}
                {cfg.label}
              </span>
            ))}
          </div>

          {error && (
            <div className="mb-3 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-600">
              {error}
            </div>
          )}

          <button
            onClick={handleAnalyze}
            disabled={analyzing || readyDocs.length < 2}
            className={`w-full sm:w-auto flex items-center justify-center gap-2 px-6 py-3 rounded-xl font-semibold text-sm transition-all ${
              analyzing || readyDocs.length < 2
                ? "bg-gray-200 text-gray-400 cursor-not-allowed"
                : "bg-blue-600 hover:bg-blue-700 text-white shadow-sm hover:shadow-md"
            }`}
          >
            {analyzing ? (
              <>
                <RefreshCw className="w-4 h-4 animate-spin" />
                Claude AI 분석 중...
              </>
            ) : (
              <>
                <Play className="w-4 h-4" />
                발주도서 검토 시작
              </>
            )}
          </button>
          {readyDocs.length < 2 && (
            <p className="text-xs text-gray-400 mt-1.5">
              최소 2개 문서를 업로드해야 비교 분석이 가능합니다 (현재 {readyDocs.length}개)
            </p>
          )}
        </section>

        {/* 분석 결과 */}
        {result && (
          <section id="results">
            <div className="flex items-center gap-2 mb-4">
              <span className="w-6 h-6 rounded-full bg-green-600 text-white text-xs font-bold flex items-center justify-center flex-shrink-0">
                ✓
              </span>
              <h2 className="font-semibold text-gray-800">검토 결과</h2>
              <span className="text-xs text-gray-400">
                세션 ID: {result.session_id.slice(0, 8)}
              </span>
            </div>
            <AnalysisResults result={result} />
          </section>
        )}
      </main>

      <footer className="mt-12 border-t border-gray-200 py-6">
        <div className="max-w-5xl mx-auto px-4 text-center text-xs text-gray-400">
          건설공사 발주도서 검토 툴 · Powered by Claude Opus 4.6
        </div>
      </footer>
    </div>
  );
}
