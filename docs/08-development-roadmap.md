# 08. 개발 로드맵

> 각 단계: 개발 범위 / 완료 조건 / 기존 시스템 비교 검증 항목 / 테스트 항목

## 단계 0 — 프로젝트 기반 구축 (현재 상태)

- **개발 범위**:
  - Solution 구조 확정 (6개 프로젝트)
  - 프로젝트 간 참조 관계 확정 (Domain ← Application ← Infrastructure/Documents ← Desktop)
  - NuGet 패키지 추가: `CommunityToolkit.Mvvm`, `Microsoft.EntityFrameworkCore.Sqlite`, `ClosedXML` 등
  - `CafeteriaDbContext` + 첫 EF Core 마이그레이션 (20개 테이블)
  - DI 컨테이너 구성, App 시작 시 마이그레이션 + 시드 (admin, 중식/석식 기본값)
- **완료 조건**:
  - `dotnet build KpicCafeteria.slnx` 성공
  - `dotnet test` 성공 (빈 테스트)
  - 앱 시작 시 DB 생성 + 기본 데이터 확인
- **기존 시스템 비교 검증**: 해당 없음 (기반 구축)
- **테스트 항목**: 빌드/시드 스모크 테스트

## 단계 1 — 도메인 모델 및 데이터 계층

- **개발 범위**:
  - Domain 엔티티 20종 + 값 객체 (`CompositionKey`, `MealType`, `OrderStatus`, 수량 환산 규칙)
  - EF Core 매핑 (스냅샷 컬럼, JSON 컬럼, unique 제약, FK SET NULL)
  - 리포지토리 인터페이스/구현
  - PBKDF2 해셔 (기존 포맷 호환)
- **완료 조건**:
  - 기존 `models.py`의 모든 테이블/제약이 EF Core에 매핑됨
  - 기존 SQLite `cafeteria.db`를 읽어 동일 데이터가 조회됨 (읽기 호환 검증)
- **기존 시스템 비교 검증**:
  - `models.py` 20개 테이블 컬럼/제약 대조
  - 기존 DB 덤프를 신규 DbContext로 조회 (행 수, 대표 값)
- **테스트 항목**:
  - `DomainModelTests`: composition_key 생성/비교, 수량 환산 공식
  - `DbContextTests`: 테이블 생성, unique 제약, cascade/SET NULL 동작
  - `PasswordHasherTests`: 기존 해시 검증 (기존 DB의 admin 해시로 로그인)

## 단계 2 — 기준정보 (Master Data)

- **개발 범위**:
  - `MasterDataService`: 메뉴/재료/별칭/레시피 CRUD
  - 레시피 다중 버전, composition_key 중복 검증, 기본 레시피 대체
  - 배식 기본값 관리
  - WPF: `MasterMenuView`(메뉴·레시피 + 재료 그리드), `MasterIngredientView`, `MealDefaultsView`
- **완료 조건**:
  - 메뉴/재료 등록·수정·미사용 처리, 다중 레시피 등록/변경/기본 지정이 화면에서 동작
  - 기존 `test_multi_recipe.py`의 규칙(구성 분리, 수량 변경 시 기존 수정, 중복 409)이 C# 테스트로 통과
- **기존 시스템 비교 검증**:
  - `test_multi_recipe.py` 시나리오를 C#으로 이식해 동일 결과 확인
  - 메뉴 이름 중복 409, 재료 자동 등록(`자동등록-분류필요`) 동작 확인
- **테스트 항목**: `RecipeTests`, `MasterDataServiceTests` (중복/미사용/기본 레시피 대체)

## 단계 3 — 주간 급식 운영 (Workspace)

- **개발 범위**:
  - `MealServiceService`: 주간 조회, 배식 CRUD, 메뉴 추가(단건/일괄), 레시피 변경, 식단 편집 일괄 저장, 재료 스냅샷 편집, 순서/삭제
  - 보존식 기록, 실제 식수
  - 조리지시서 작성
  - WPF: `WorkspaceView` (주간 보드 + 우측 패널, 모드 탭), `MenuPickerView`
  - `TimeInput24` 컨트롤
- **완료 조건**:
  - 주간 2주 화면에서 식단 작성/수정/삭제, 레시피 선택, 재료 편집, 보존식/실제식수 입력이 전부 동작
  - 계획식수 변경 시 quantity_total 재계산 확인
- **기존 시스템 비교 검증**:
  - `test_meal_editor.py` 시나리오 이식 (콘셉트 저장, 재료 교체, 빈 재료 삭제, per_100 역산)
  - `test_menu_picker.py` 시나리오 이식 (중복 400/409, 비활성 404, 다른 메뉴 레시피 400, 스냅샷 복사)
  - 스냅샷 원칙: 기준 레시피 수정 후 과거 식단 재료 불변 확인
- **테스트 항목**: `MealServiceTests`, `MenuPickerTests`, `TimeInput24Tests`

## 단계 4 — 발주 관리 (Orders)

- **개발 범위**:
  - `OrderService`: 집계 조회, 항목 upsert, 묶음 발주, 일괄 변경
  - WPF: `OrdersView` (재료별/사용일별 탭, 인라인 편집, 묶음/일괄 바)
- **완료 조건**:
  - 식단 기반 필요량 집계, 발주량 수정, 묶음 발주, 상태 변경이 화면에서 동작
  - 식단 변경 후에도 사용자 발주 입력이 보존됨
- **기존 시스템 비교 검증**:
  - `test_orders.py` 전체 시나리오 이식 (동일 날짜 집계, id/name 키, 날짜별 분리, 입력 보존, in_plan=false 유지, 묶음/일괄)
- **테스트 항목**: `OrderServiceTests` (6개 시나리오)

## 단계 5 — 문서 시스템 (HWPX/PDF/Excel)

- **개발 범위**:
  - Documents 프로젝트: `HwpxPackage`, `HwpxTemplateEngine`, 렌더러 3종, 반복 페이지, 검증
  - `HancomComPdfRenderer` (COM)
  - `ExcelArchiveExporter` (9시트)
  - 템플릿 관리 UI
  - 문서 생성 UI (식단표/조리지시서/보존식 기록지)
- **완료 조건**:
  - 기존 `docs/template/*.hwpx` 3종으로 HWPX 생성 성공
  - 생성된 HWPX에 `{{...}}` 잔존 없음, ZIP/XML 무결성
  - 한컴오피스 설치 환경에서 PDF 변환 성공
- **기존 시스템 비교 검증**:
  - `test_hwpx_repeat_pages.py` 시나리오 이식: 식단표 2/3/4/5/6주 → 페이지 수 `ceil(주/2)`, 조리지시서 1/2/5/10일 → 일자 수, 보존식 1/3/4/6/7/10식 → `ceil(식수/3)`
  - `test_hwpx_engine.py` round-trip (ZIP 무결성, 플레이스홀더 제거)
  - `test_hwpx_output_system.py` 실제 데이터셋 (희소 데이터, 대량 재료, 특수문자)
  - **수동 검증**: 생성 HWPX를 한글에서 열어 복구 메시지/깨짐/편집 가능 여부 확인 (기존 reference PDF와 비교)
- **테스트 항목**: `HwpxEngineTests`, `HwpxRepeatPagesTests`, `HwpxOutputSystemTests`, `PdfRendererTests` (Fake), `ExcelArchiveTests`

## 단계 6 — 통계/대시보드

- **개발 범위**:
  - `StatisticsService`: 식수/메뉴/식재료/운영 통계, 대시보드 집계
  - WPF: `DashboardView`, `StatisticsViews` 4종 + 차트
- **완료 조건**:
  - 기간 선택 통계가 기존과 동일한 수치를 반환
  - 이상치(±10%/±15%, 56일 중앙값), 반복(14일 2회/28일 3회), 미사용(90일) 판정 동작
- **기존 시스템 비교 검증**:
  - 동일 데이터셋(기존 DB)으로 기존 API 응답과 C# 결과를 대조 (수치 일치)
  - 경계값: 편차 9.9%/10.0%/15.0%, 비교 데이터 3건/4건
- **테스트 항목**: `StatisticsServiceTests` (판정 경계값 포함)

## 단계 7 — 이관/백업/아카이브/사용자

- **개발 범위**:
  - `ImportService`: XLSX 이관 (7시트, replace/merge)
  - `BackupService`: SQLite 파일 백업 + SHA-256 + 목록
  - `ArchiveService`: Excel 아카이브 + 24시간 만료
  - `UserService`: 사용자 관리, 비밀번호 변경/초기화
  - WPF: `SetupImportView`, `BackupView`, `ArchiveView`, `UsersView`, `LoginView`
- **완료 조건**:
  - 기존 이관 XLSX로 replace/merge 이관 성공 (기존 `test_core.py` 기준: 배식 2건, 메뉴/재료/식단이력 행 수)
  - 기존 PostgreSQL 데이터를 XLSX 경유로 신규 SQLite에 이관 가능
  - 백업/아카이브 생성·다운로드·삭제 동작
- **기존 시스템 비교 검증**:
  - `test_core.py` 시나리오 이식 (시트 수, 레시피 구성 그룹핑, 수량·단위 무시)
  - 이관 후 과거 식단 스냅샷 수량 보존 확인
- **테스트 항목**: `ImportServiceTests`, `BackupServiceTests`, `ArchiveServiceTests`, `UserServiceTests`

## 단계 8 — 통합 검증 및 배포 준비

- **개발 범위**:
  - 전체 워크플로 통합 테스트 (기준정보 → 식단 → 조리지시 → 보존식 → 실제식수 → 발주 → 문서 → 통계)
  - 설치 패키지 (MSIX 또는 ClickOnce/Inno Setup)
  - 사용자 매뉴얼
- **완료 조건**:
  - 기존 시스템의 README "직접 확인 권장 항목" 전부 통과
  - 설치형 배포물 생성
- **기존 시스템 비교 검증**:
  - README 검증 목록: XLSX 교체/병합 이관, 월~금 5열, 집중 작성 모드, 식단 메뉴/재료 수정, 선택적 조리지시 저장, 보존식 완료 상태, 실제 식수 별도 저장, HTML/PDF 문서, HWPX 템플릿 등록 및 한글 열기
  - 기존 DB 전체 데이터 이관 후 통계 수치 일치
- **테스트 항목**: 전체 테스트 스위트, 수동 UAT 체크리스트

## 기존 테스트를 동등성 검증 기준으로 활용하는 방법

1. **규칙 단위 이식**: 각 pytest 파일의 시나리오를 xUnit `[Fact]`/`[Theory]`로 1:1 이식. 테스트 이름에 기존 테스트명을 주석으로 남겨 추적.
2. **동일 입력/출력 대조**: 이식 테스트에 동일한 입력 데이터를 넣고 기존 시스템(또는 기존 테스트가 검증한 값)과 동일한 결과를 단언.
3. **경계값 보강**: 기존 테스트가 다루지 않은 경계값(편차 10%/15%, 페이지 수 경계, 빈 데이터)을 추가.
4. **실제 템플릿 회귀**: `docs/template/*.hwpx` 원본 파일을 테스트 리소스로 복사해 반복 페이지 테스트 수행 (파일 해시로 원본과 동일함을 확인).
5. **수동 검증 병행**: HWPX 편집 가능성/한글 열림은 자동 테스트로 대체 불가 — 단계 5 완료 조건에 수동 체크리스트 포함.
6. **데이터 이관 검증**: 기존 DB → 신규 DB 이관 후 행 수/대표 값/스냅샷 무결성을 자동 비교하는 통합 테스트.

## 리스크 완화

- **HWPX 엔진**: 단계 5에서 기존 템플릿 3종 + 반복 페이지 테스트를 최우선으로 수행. 실패 시 단계 5를 블로킹.
- **한컴오피스 COM**: 개발 환경에 한컴오피스 설치 확인. 미설치 시 Fake 렌더러로 파이프라인만 검증하고 수동 검증 연기.
- **기존 데이터 이관**: 단계 1에서 읽기 호환 검증을 먼저 수행해 스키마 오류를 조기 발견.
