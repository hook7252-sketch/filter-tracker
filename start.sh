#!/bin/bash
# 건설공사 발주도서 검토 툴 실행 스크립트

set -e

echo "=== 건설공사 발주도서 검토 툴 ==="

# 백엔드 실행
echo ""
echo "[1/2] 백엔드 서버 시작 (http://localhost:8000)"
cd backend

if [ ! -f ".env" ]; then
  if [ -z "$ANTHROPIC_API_KEY" ]; then
    echo "⚠️  ANTHROPIC_API_KEY 환경변수가 설정되지 않았습니다."
    echo "    export ANTHROPIC_API_KEY=your_key_here"
    exit 1
  fi
fi

# 가상환경 확인
if [ ! -d "venv" ]; then
  echo "가상환경 생성 중..."
  python3 -m venv venv
fi

source venv/bin/activate
pip install -r requirements.txt -q

uvicorn main:app --host 0.0.0.0 --port 8000 --reload &
BACKEND_PID=$!
echo "백엔드 PID: $BACKEND_PID"

cd ..

# 프론트엔드 실행
echo ""
echo "[2/2] 프론트엔드 서버 시작 (http://localhost:3000)"
cd frontend

if [ ! -d "node_modules" ]; then
  echo "패키지 설치 중..."
  npm install
fi

cp -n .env.local.example .env.local 2>/dev/null || true
npm run dev &
FRONTEND_PID=$!
echo "프론트엔드 PID: $FRONTEND_PID"

echo ""
echo "✅ 서버 실행 완료!"
echo "   프론트엔드: http://localhost:3000"
echo "   백엔드 API: http://localhost:8000"
echo "   API 문서:   http://localhost:8000/docs"
echo ""
echo "종료하려면 Ctrl+C"

# 종료 처리
cleanup() {
  echo ""
  echo "서버 종료 중..."
  kill $BACKEND_PID 2>/dev/null
  kill $FRONTEND_PID 2>/dev/null
  exit 0
}
trap cleanup SIGINT SIGTERM

wait
