import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "건설공사 발주도서 검토 툴",
  description: "AI 기반 설계도면·수량산출서·내역서 상호 비교 및 표준품셈 검토 시스템",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ko">
      <body className="antialiased">
        {children}
      </body>
    </html>
  );
}
