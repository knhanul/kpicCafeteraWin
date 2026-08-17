# 01. 현재 시스템 전체 구조 및 기술 구성 분석

> 분석 기준: `C:\Pjt\kpicCafeteria` (Reference Implementation, 수정 금지)
> 작성일: 2026-08-15

## 1. 개요

현재 시스템은 **영양사 1인용 구내식당 관리 시스템**으로, 식재료 마이그레이션 XLSX를 업로드하여 기초데이터와 과거 식단을 생성하고, 한 화면에서 식단·조리지시·보존식·실제 식수를 관리하는 웹 애플리케이션이다.

- **형태**: FastAPI 기반 서버 렌더링 + Vanilla JavaScript SPA
- **배포**: Docker Compose (app + PostgreSQL 16 + Nginx)
- **DB**: PostgreSQL 16 (개발/테스트는 SQLite 호환)
- **문서 출력**: HTML 미리보기 / HWPX (한컴오피스 양식) / PDF (한컴오피스 COM 변환)

## 2. 기술 스택

| 영역 | 기술 | 버전 | 비고 |
| --- | --- | --- | --- |
| 웹 프레임워크 | FastAPI | 0.116.1 | `/api/docs` Swagger 제공 |
| ORM | SQLAlchemy 2 | 2.0.41 | Declarative Base, `Mapped`/`mapped_column` |
| DB 드라이버 | psycopg | 3.2.9 | PostgreSQL |
| DB | PostgreSQL 16 | - | Docker Compose |
| 개발/테스트 DB | SQLite | - | `sqlite:///./cafeteria.db` |
| 템플릿 | Jinja2 | 3.1.6 | 화면(app.html) 및 문서 HTML |
| 프론트엔드 | Vanilla JavaScript | - | `app.js` 약 200KB 단일 파일, 빌드 없음 |
| CSS | 커스텀 | - | `app.css` 약 45KB |
| PDF 변환 | 한컴오피스 COM | - | `HWPFrame.HwpObject` → SaveAs PDF |
| XLSX 읽기 | 표준 라이브러리 전용 | - | `SimpleXlsxReader` (openpyxl은 아카이브 생성에만 사용) |
| XLSX 쓰기 | openpyxl | 3.1.5 | Excel 아카이브 생성 |
| 테스트 | pytest | 8.4.1 | 13개 테스트 파일 |
| 인증 | 세션 쿠키 | - | `SessionMiddleware` (itsdangerous) |
| 비밀번호 | PBKDF2-SHA256 | - | 240,000 iterations, `pbkdf2_sha256$...` 포맷 |

## 3. 디렉터리 구조

```text
kpicCafeteria/
├─ backend/
│  ├─ app/
│  │  ├─ main.py                  # FastAPI 앱, startup 시 스키마 업그레이드 + 기본 데이터 생성
│  │  ├─ config.py                # pydantic-settings 기반 환경설정
│  │  ├─ db.py                    # engine / SessionLocal / Base
│  │  ├─ deps.py                  # current_user / admin_user 의존성
│  │  ├─ security.py              # PBKDF2 비밀번호 해시
│  │  ├─ models.py                # SQLAlchemy 모델 전체 (20개 테이블)
│  │  ├─ serializers.py           # API 응답 직렬화 (meal_service_dict 등)
│  │  ├─ schema_upgrade.py        # 기존 스키마 인플레이스 업그레이드
│  │  ├─ importer.py              # XLSX 마이그레이션 (preview/apply)
│  │  ├─ xlsx_reader.py           # 표준 라이브러리 XLSX 파서
│  │  ├─ stats_service.py         # 주간/대시보드 참고 통계 (legacy)
│  │  ├─ statistics_service.py    # 식수 통계 (편차/이상치)
│  │  ├─ menu_statistics.py       # 메뉴 통계
│  │  ├─ ingredient_statistics.py # 식재료 통계
│  │  ├─ operations_statistics.py # 운영 기록 통계
│  │  ├─ dashboard_service.py     # 운영 대시보드 집계
│  │  ├─ document_service.py      # 문서 payload 생성 + 미리보기 토큰
│  │  ├─ document_dtos.py         # 문서 출력 전용 DTO (pydantic)
│  │  ├─ document_builders.py     # ORM → 문서 DTO 변환
│  │  ├─ document_hwpx.py         # DTO → HWPX/PDF bytes
│  │  ├─ hwpx_engine.py           # HWPX 템플릿 엔진 (ZIP/XML 조작, 반복 페이지)
│  │  ├─ hwpx_service.py          # HWPX 검증/활성 템플릿 조회 (구버전 래퍼)
│  │  ├─ hwpx_pdf_renderer.py     # 한컴오피스 COM 기반 HWPX→PDF
│  │  ├─ routers/
│  │  │  ├─ auth.py               # 로그인/로그아웃/비밀번호 변경
│  │  │  ├─ setup.py              # XLSX 이관 preview/apply/jobs
│  │  │  ├─ master.py             # 메뉴/재료/레시피/별칭 기준정보
│  │  │  ├─ workspace.py          # 주간 식단/조리지시/보존식/실제식수
│  │  │  ├─ orders.py             # 발주 관리
│  │  │  ├─ documents.py          # 문서 미리보기/PDF/HWPX 다운로드
│  │  │  ├─ templates.py          # HWPX 템플릿 등록 (구버전)
│  │  │  ├─ master_data.py        # HWPX 템플릿 관리 + 배식 기본값 (신버전)
│  │  │  ├─ statistics.py         # 통계 API (dashboard/menus/ingredients/operations/meals)
│  │  │  ├─ stats.py              # 주간/대시보드 참고 통계 API (legacy)
│  │  │  ├─ users.py              # 사용자 관리 (admin)
│  │  │  └─ admin.py              # 백업/아카이브 (admin)
│  │  ├─ static/
│  │  │  ├─ app.js                # SPA 전체 로직 (약 200KB)
│  │  │  └─ app.css
│  │  └─ templates/
│  │     ├─ app.html              # 메인 화면 (좌측 네비 + 뷰 컨테이너)
│  │     ├─ login.html
│  │     └─ documents/            # 문서 HTML 미리보기 템플릿 3종
│  ├─ tests/                      # pytest 13개 파일
│  ├─ cafeteria.db                # SQLite 개발 DB
│  └─ requirements.txt
├─ docs/
│  ├─ ARCHITECTURE.md / DATA_MODEL.md / IMPORT_FORMAT.md / OPERATIONS.md
│  ├─ focus-mode-layout-analysis.md
│  ├─ hwpx-output-analysis.md / hwpx-output-system.md / hwpx-pdf-renderer.md
│  ├─ reference/                  # 출력물 PDF 샘플 3종 (식단표/조리지시서/보존식기록지)
│  └─ template/                   # 반복 페이지 HWPX 템플릿 3종 + page config JSON
├─ data/                          # 백업/아카이브 출력
├─ storage/                       # 업로드/템플릿/생성파일
├─ scripts/                       # start/backup 스크립트
├─ nginx/                         # 리버스 프록시 설정
└─ docker-compose.yml
```

## 4. 화면 구성 (app.html)

단일 페이지 앱으로 좌측 사이드바 네비게이션 + 우측 뷰 컨테이너 구조.

| 뷰 | 화면 | 주요 기능 |
| --- | --- | --- |
| `workspace` | 주간 급식 운영 | 좌측 월~금 주간 식단표(2주), 우측 업무 패널. 모드 탭: 식단 작성 / 조리지시서 / 보존식 기록 / 실제 식수. 집중 작성 모드 지원 |
| `orders` | 발주 관리 | 기간 조회, 재료별/사용일별 보기, 발주량·발주일·배송일·상태 인라인 편집, 묶음 발주, 일괄 변경 |
| `master` | 메뉴·재료 기준정보 | 메뉴·레시피 탭 / 재료 탭. 목록 + 우측 편집 패널, 레시피 재료 엑셀형 그리드 |
| `master-data` | 기본 데이터 관리 | HWPX 양식 관리 / 배식 기본값 관리 / 기초 데이터 구축(XLSX 이관) |
| `dashboard` | 운영 대시보드 | KPI, 추세, 이상치(식수 급감/급증, 메뉴 반복, 재료 변화, 기록 누락) |
| `stats-meals` | 식수 통계 | 계획·실제 식수, 편차, 평소 대비, 이상치 |
| `stats-menus` | 메뉴 통계 | 사용 횟수, 반복, 미사용, 상세 |
| `stats-ingredients` | 식재료 통계 | 재료군/개별 사용량, 미사용, 상세 |
| `stats-operations` | 운영 기록 통계 | 식단/조리지시/보존식/실제식수 완료율 |
| `users` | 사용자 관리 (admin) | 계정 CRUD, 비밀번호 초기화 |
| `backup` | 시스템 데이터 백업 (admin) | pg_dump 수동 백업, 목록/다운로드/삭제 |
| `archive` | Excel 데이터 아카이브 (admin) | 기간별 Excel 아카이브 생성/다운로드 |

## 5. API 엔드포인트 전체 목록

### 인증 (`/api/auth`)
- `POST /api/auth/login` — 로그인 (세션 쿠키)
- `POST /api/auth/logout` — 로그아웃
- `GET /api/auth/me` — 현재 사용자
- `POST /api/auth/change-password` — 비밀번호 변경

### 기초 데이터 이관 (`/api/setup`)
- `POST /api/setup/import/preview` — XLSX 업로드 + 검증
- `POST /api/setup/import/apply` — 이관 적용 (mode: replace/merge)
- `GET /api/setup/import/jobs` — 이관 이력

### 기준정보 (`/api/master`)
- `GET /api/master/codes` — 메뉴역할/통계분석군/단위 코드 목록
- `GET/POST /api/master/menus`, `GET/PUT/DELETE /api/master/menus/{id}`
- `GET /api/master/menus/picker` — 식단 추가용 메뉴 선택기
- `POST /api/master/menus/{id}/recipes`, `PUT/DELETE /api/master/recipes/{id}`, `POST /api/master/recipes/{id}/default`
- `PUT /api/master/menus/{id}/recipe` — 구버전 호환 (기본 레시피 갱신)
- `GET/POST /api/master/ingredients`, `GET/PUT/DELETE /api/master/ingredients/{id}`
- `POST /api/master/ingredients/{id}/aliases`

### 주간 작업공간 (`/api/workspace`)
- `GET /api/workspace/weeks?week_start=&weeks=` — 주간 식단 조회 (2주 기본)
- `POST /api/workspace/services` — 배식 생성 (평일만, 중복 시 기존 반환)
- `GET/PUT/DELETE /api/workspace/services/{id}`
- `POST /api/workspace/services/{id}/menus` — 메뉴 추가 (레시피 선택)
- `POST /api/workspace/services/{id}/menus/batch` — 메뉴 일괄 추가
- `PUT /api/workspace/service-menus/{id}/recipe` — 레시피 변경
- `PUT /api/workspace/service-menus/{id}` — 비고/대표메뉴/조리지시 저장
- `PUT /api/workspace/service-menus/{id}/ingredients` — 재료 스냅샷 교체
- `PUT /api/workspace/services/{id}/meal-editor` — 식단 편집 일괄 저장
- `DELETE /api/workspace/service-menus/{id}` — 메뉴 삭제 (정렬 재계산)
- `POST /api/workspace/services/{id}/reorder` — 메뉴 순서 변경
- `GET/PUT /api/workspace/services/{id}/preservation` — 보존식 기록
- `GET/PUT /api/workspace/services/{id}/actual` — 실제 식수

### 발주 (`/api/orders`)
- `GET /api/orders?start_date=&end_date=` — 발주 목록 (식단 집계 + 저장값 병합)
- `PUT /api/orders/items` — 발주 항목 upsert
- `POST /api/orders/group` — 묶음 발주 생성
- `PUT /api/orders/bulk` — 일괄 변경 (발주일/배송일/상태)

### 문서 (`/api/documents`)
- `POST /api/documents/preview` — 미리보기 토큰 생성
- `GET /preview/{token}` — HTML 미리보기 페이지
- `GET /api/documents/{token}/pdf` — 토큰 기반 PDF
- `GET /api/documents/{token}/hwpx` — 토큰 기반 HWPX
- `POST /api/documents/{type}/preview` — 기간 기반 PDF 미리보기 (meal-plan/cooking-instruction/preserved-food/preservation-record)
- `POST /api/documents/{type}/hwpx` — 기간 기반 HWPX 다운로드

### HWPX 템플릿 관리 (`/api/master-data`, `/api/templates`)
- `GET/POST /api/master-data/document-templates`, `GET/PUT/DELETE /{id}`
- `POST /api/master-data/document-templates/{id}/validate|activate|deactivate`
- `GET /api/master-data/document-templates/{id}/download`
- `GET/POST /api/templates`, `POST /{id}/activate`, `DELETE /{id}` (구버전)

### 배식 기본값 (`/api/master-data`)
- `GET/PUT /api/master-data/meal-service-defaults`

### 통계 (`/api/statistics`, `/api/stats`)
- `GET /api/statistics/dashboard?start_date=&end_date=&meal_type=`
- `GET /api/statistics/menus`, `GET /api/statistics/menus/{id}`
- `GET /api/statistics/ingredients`, `GET /api/statistics/ingredients/{id}`
- `GET /api/statistics/operations`
- `GET /api/statistics/meals`, `GET /api/statistics/meals/trend`
- `GET /api/stats/week?week_start=`, `GET /api/stats/dashboard` (legacy)

### 사용자 (`/api/users`, admin)
- `GET/POST /api/users`, `PUT /api/users/{id}`, `POST /api/users/{id}/reset-password`

### 시스템 (`/api/admin`, admin)
- `GET/POST /api/admin/backups`, `GET /{id}/download`, `DELETE /{id}`
- `GET/POST /api/admin/archives`, `GET /{id}/download`, `DELETE /{id}`, `POST /api/admin/archives/cleanup`

## 6. 데이터 흐름 예시

### 식단 작성 (메뉴 추가)
```text
주간 화면 (app.js)
→ POST /api/workspace/services/{id}/menus/batch
→ routers/workspace.py batch_add_menus()
→ _select_recipe() (기본 레시피 선택 규칙)
→ _copy_recipe_to_service_menu() (레시피 재료 → 스냅샷 복사, quantity_total 계산)
→ models.MealServiceMenu / MealServiceMenuIngredient
→ DB
```

### 발주 조회
```text
발주 화면 (app.js)
→ GET /api/orders?start_date=&end_date=
→ routers/orders.py list_orders()
→ _load_plan_items() (식단 재료를 (사용일, 재료) 기준 집계)
→ _load_stored_items() (저장된 OrderItem 로드)
→ 병합: 식단 기준 required + 저장된 order_quantity/status 보존
→ DB
```

### 문서 출력 (HWPX)
```text
출력물 생성 버튼 (app.js)
→ POST /api/documents/meal-plan/hwpx
→ routers/documents.py _download_hwpx_by_type()
→ document_hwpx.generate_hwpx_bytes()
→ document_builders (ORM → DTO)
→ hwpx_engine.render_document() (템플릿 로드 → 반복 페이지 복제 → 플레이스홀더 치환)
→ HWPX bytes 반환
→ (PDF는 동일 HWPX bytes를 한컴오피스 COM으로 변환)
```

## 7. 주요 설계 원칙 (README/OPERATIONS.md에서 확인)

- 영양사 1인이 제약 없이 사용 — 승인·확정·과거 데이터 잠금 없음
- 수정 제한보다 백업·삭제 확인·스냅샷 제공
- 조리지시는 메뉴별 필수 입력이 아님
- 실제 식수는 보존식 기록과 별도 저장
- 통계는 별도 대시보드에서 참고 정보만 제공 (선택 차단 없음)
- 메뉴/재료 삭제는 과거 스냅샷 보존을 위해 **미사용 처리** (물리 삭제 아님)
- 과거 이관 데이터도 일반 데이터처럼 수정 가능
- HWPX는 템플릿의 표·행 높이·셀 병합·글꼴 보존 (패키지 재생성 아님)

## 8. 미확인 사항

- `docs/template/TEMPLATE_FIELDS.md` 파일이 참조되나 현재 저장소에 존재하지 않음 (hwpx-output-system.md의 참조만 확인)
- `data/source/식재료_마이그레이션_기준정보.xlsx` 기준 파일은 테스트에서 참조하나 저장소에 존재하지 않음 (`.gitignore` 대상으로 추정)
- `scripts/start.ps1`, `scripts/backup.ps1` 내용 미확인 (scripts 디렉터리 비어 있음)
- `templates/hwpx/README.md` (README에서 참조) 미확인
