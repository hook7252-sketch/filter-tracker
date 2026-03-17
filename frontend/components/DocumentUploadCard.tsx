"use client";

import { useState, useRef, DragEvent } from "react";
import { Upload, FileText, Trash2, CheckCircle, AlertCircle, Loader2 } from "lucide-react";
import type { DocumentType, UploadedDocument } from "@/lib/api";

interface Props {
  docType: DocumentType;
  label: string;
  description: string;
  acceptedFormats: string;
  color: string;
  document?: UploadedDocument;
  onUpload: (file: File, docType: DocumentType) => Promise<void>;
  onDelete: (docId: string) => Promise<void>;
}

const colorMap = {
  blue: {
    border: "border-blue-300 hover:border-blue-500",
    bg: "bg-blue-50",
    badge: "bg-blue-100 text-blue-700",
    icon: "text-blue-500",
    active: "border-blue-500 bg-blue-50",
  },
  green: {
    border: "border-green-300 hover:border-green-500",
    bg: "bg-green-50",
    badge: "bg-green-100 text-green-700",
    icon: "text-green-500",
    active: "border-green-500 bg-green-50",
  },
  orange: {
    border: "border-orange-300 hover:border-orange-500",
    bg: "bg-orange-50",
    badge: "bg-orange-100 text-orange-700",
    icon: "text-orange-500",
    active: "border-orange-500 bg-orange-50",
  },
};

export default function DocumentUploadCard({
  docType, label, description, acceptedFormats, color,
  document, onUpload, onDelete,
}: Props) {
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const c = colorMap[color as keyof typeof colorMap] || colorMap.blue;

  const handleFile = async (file: File) => {
    setUploading(true);
    try {
      await onUpload(file, docType);
    } finally {
      setUploading(false);
    }
  };

  const handleDrop = (e: DragEvent) => {
    e.preventDefault();
    setDragging(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFile(file);
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) handleFile(file);
    e.target.value = "";
  };

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
      {/* Header */}
      <div className={`px-4 py-3 ${c.bg} border-b border-gray-100`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FileText className={`w-4 h-4 ${c.icon}`} />
            <span className="font-semibold text-gray-800">{label}</span>
          </div>
          <span className={`text-xs px-2 py-0.5 rounded-full ${c.badge}`}>
            {acceptedFormats}
          </span>
        </div>
        <p className="text-xs text-gray-500 mt-1">{description}</p>
      </div>

      {/* Content */}
      <div className="p-4">
        {document ? (
          /* 업로드 완료 상태 */
          <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50 border border-gray-200">
            <div className="flex items-center gap-3 min-w-0">
              {document.status === "done" ? (
                <CheckCircle className="w-5 h-5 text-green-500 flex-shrink-0" />
              ) : document.status === "error" ? (
                <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0" />
              ) : (
                <Loader2 className="w-5 h-5 text-blue-500 animate-spin flex-shrink-0" />
              )}
              <div className="min-w-0">
                <p className="text-sm font-medium text-gray-700 truncate">{document.filename}</p>
                {document.status === "error" && (
                  <p className="text-xs text-red-500">{document.error}</p>
                )}
                {document.status === "done" && (
                  <p className="text-xs text-green-600">파싱 완료</p>
                )}
              </div>
            </div>
            <button
              onClick={() => onDelete(document.id)}
              className="p-1.5 rounded-md hover:bg-red-50 text-gray-400 hover:text-red-500 transition-colors flex-shrink-0"
              title="삭제"
            >
              <Trash2 className="w-4 h-4" />
            </button>
          </div>
        ) : (
          /* 드래그앤드롭 업로드 영역 */
          <div
            className={`border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-all ${
              dragging ? c.active : c.border
            } ${uploading ? "opacity-60 pointer-events-none" : ""}`}
            onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
            onDragLeave={() => setDragging(false)}
            onDrop={handleDrop}
            onClick={() => inputRef.current?.click()}
          >
            {uploading ? (
              <div className="flex flex-col items-center gap-2">
                <Loader2 className={`w-8 h-8 animate-spin ${c.icon}`} />
                <p className="text-sm text-gray-500">업로드 중...</p>
              </div>
            ) : (
              <div className="flex flex-col items-center gap-2">
                <Upload className={`w-8 h-8 ${c.icon}`} />
                <p className="text-sm font-medium text-gray-600">
                  파일을 드래그하거나 클릭하여 업로드
                </p>
                <p className="text-xs text-gray-400">{acceptedFormats}</p>
              </div>
            )}
          </div>
        )}
      </div>

      <input
        ref={inputRef}
        type="file"
        className="hidden"
        accept={acceptedFormats.split(", ").map(f => `.${f.replace(".", "")}`).join(",")}
        onChange={handleChange}
      />
    </div>
  );
}
