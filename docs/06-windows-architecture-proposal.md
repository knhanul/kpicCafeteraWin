# 06. Windows 버전 권장 아키텍처 (.NET 10 + WPF)

## 1. 기술 선택 제안

| 영역 | 선택 | 근거 |
| --- | --- | --- |
| 런타임 | .NET 10 | 요구사항 |
| UI | WPF (MVVM) | 요구사항. Windows Native |
| MVVM 프레임워크 | CommunityToolkit.Mvvm 8.x | 경량, 소스 생성기 기반, WPF 표준 |
| DB | **SQLite (EF Core)** | 단일 영양사 1인 사용, 설치형 데스크톱, 백업=파일 복사. PostgreSQL은 서버 운영 필요 |
| ORM | EF Core 10 | 요구사항 |
| 문서 생성 | KpicCafeteria.Documents 전용 프로젝트 | HWPX 엔진 C# 이식 + 한컴오피스 COM |
| Excel | ClosedXML 또는 EPPlus | 아카이브/이관. (기존 openpyxl 대응) |
| XLSX 읽기 | ClosedXML (이관용) | 기존 SimpleXlsxReader 대체 |
| PDF 변환 | 한컴오피스 COM (HwpObject) | 기존과 동일 방식, Windows 네이티브에서 안정적 |
| DI | Microsoft.Extensions.DependencyInjection | 표준 |
| 로깅 | Microsoft.Extensions.Logging | 표준 |
| 설정 | appsettings.json + 사용자 설정 | |
| 테스트 | xUnit (이미 구성됨) | |
| 인증 | 로컬 사용자 + PBKDF2 (기존 해시 호환) | 기존 users 테이블 이관 호환 |

> **DB 선택 근거**: 기존 시스템은 PostgreSQL 16이지만, Windows 데스크톱 단일 사용자에게는 SQLite가 적합하다. 단, 기존 PostgreSQL 데이터 이관을 위해 **이관 도구(Import/Export)**를 1단계에서 제공한다. EF Core는 SQLite/PostgreSQL 모두 지원하므로 Provider 교체만으로 확장 가능하게 설계한다.

## 2. 프로젝트 구조와 책임

```text
KpicCafeteria.slnx
├─ src/
│  ├─ KpicCafeteria.Domain          # 순수 도메인 (의존성 없음)
│  ├─ KpicCafeteria.Application     # 애플리케이션 서비스/유스케이스
│  ├─ KpicCafeteria.Infrastructure  # EF Core, DB, 파일 저장, 외부 연동
│  ├─ KpicCafeteria.Documents       # 문서 생성 (HWPX/PDF/Excel)
│  └─ KpicCafeteria.Desktop         # WPF UI (MVVM)
└─ tests/
   └─ KpicCafeteria.Tests           # xUnit
```

### 2-1. KpicCafeteria.Domain (의존성 없음)
- **책임**: 엔티티, 값 객체, 도메인 규칙, 도메인 서비스, enum/코드 목록
- **주요 내용**:
  - 엔티티: `Menu`, `Ingredient`, `IngredientAlias`, `Recipe`, `RecipeIngredient`, `MealService`, `MealServiceMenu`, `MealServiceMenuIngredient`, `PreservationRecord`, `MealActual`, `OrderItem`, `OrderGroup`, `MealTypeSetting`, `User`, `DocumentTemplate`, `AuditLog`
  - 값 객체: `CompositionKey` (레시피 구성 키), `QuantityPer100`/`QuantityTotal` 환산 규칙, `MealType` (LUNCH/DINNER), `OrderStatus` (pending/ordered/skipped)
  - 도메인 규칙: R1~R24 (04-business-rules.md) 중 순수 계산 규칙 — `quantity_total = per_100 × planned / 100`, `composition_key = sorted(ingredientIds)`, 주차 라벨 계산, 이상치 판정(±10%/±15%, 56일 중앙값)
- **의존성**: 없음 (순수 C#)

### 2-2. KpicCafeteria.Application (Domain 참조)
- **책임**: 유스케이스/애플리케이션 서비스, DTO, 인터페이스 정의
- **주요 내용**:
  - 서비스: `MealServiceService`(식단), `MasterDataService`(기준정보), `OrderService`(발주), `StatisticsService`(통계), `DocumentService`(문서 요청), `ImportService`(이관), `UserService`, `BackupService`, `ArchiveService`
  - 인터페이스: `IMealServiceRepository`, `IMasterDataRepository`, `IOrderRepository`, `IDocumentTemplateRepository`, `IUnitOfWork`, `IDocumentGenerator`, `IPdfRenderer`, `IExcelExporter`, `IXlsxImporter`, `IPasswordHasher`
  - DTO: 화면/문서용 DTO (기존 serializers.py, document_dtos.py 대응)
- **의존성**: Domain만 참조

### 2-3. KpicCafeteria.Infrastructure (Domain, Application 참조)
- **책임**: EF Core DbContext/엔티티 매핑, 마이그레이션, 리포지토리 구현, 파일 저장, 한컴오피스 COM 연동(또는 Documents로 위임), 백업/아카이브 구현
- **주요 내용**:
  - `CafeteriaDbContext` — 20개 테이블 매핑 (스냅샷 컬럼, JSON 컬럼, unique 제약 포함)
  - EF Core 마이그레이션 (SQLite)
  - 리포지토리 구현
  - `Pbkdf2PasswordHasher` — 기존 `pbkdf2_sha256$240000$...` 포맷 호환 (기존 데이터 이관 시 로그인 유지)
  - 파일 저장소 (템플릿/백업/아카이브)
- **의존성**: Domain, Application

### 2-4. KpicCafeteria.Documents (Domain, Application 참조)
- **책임**: 문서 생성 전용 — HWPX 엔진, PDF 변환, Excel 생성
- **주요 내용**:
  - `HwpxPackage` / `HwpxTemplateEngine` — 기존 `hwpx_engine.py` C# 이식 (ZIP: `System.IO.Compression`, XML: `System.Xml.Linq`)
  - 렌더러: `MealPlanRenderer`, `CookingInstructionRenderer`, `PreservedFoodRenderer`
  - 반복 페이지: `RepeatPageConfig` (template-page-config.json 대응), `applyRepeatPages` 로직
  - `HancomComPdfRenderer` — `HwpObject` COM 연동 (WPF 프로세스 내 또는 별도 프로세스)
  - `ExcelArchiveExporter` — 9시트 아카이브 (ClosedXML)
  - `MigrationXlsxReader` — 이관 XLSX 파서
- **의존성**: Domain, Application (문서 DTO 규칙 재사용)

### 2-5. KpicCafeteria.Desktop (Application, Infrastructure, Documents 참조)
- **책임**: WPF UI, MVVM ViewModel, 네비게이션, 화면별 뷰
- **주요 내용**:
  - `App.xaml` — DI 컨테이너, 서비스 등록, 시작 시 DB 마이그레이션/기본 데이터 시드
  - 뷰/뷰모델: `LoginView`, `WorkspaceView`(주간 식단/조리지시/보존식/실제식수), `OrdersView`, `MasterDataView`, `DashboardView`, `StatisticsViews`, `UsersView`, `BackupView`, `ArchiveView`, `TemplateManagementView`
  - 공통: `TimeInput24` 컨트롤 (기존 JS 로직 이식), 문서 미리보기(WebView2 또는 외부 뷰어), 집중 작성 모드
- **의존성**: Application, Infrastructure, Documents

### 2-6. KpicCafeteria.Tests
- **책임**: 단위/통합 테스트. 기존 pytest 13개 파일의 검증 항목을 xUnit으로 이식
- **의존성**: Domain, Application, Infrastructure, Documents

## 3. 의존성 방향

```text
Desktop ──→ Application ──→ Domain
    │            │
    ├──→ Infrastructure ──→ Domain, Application
    └──→ Documents ──→ Domain, Application

Tests ──→ Domain, Application, Infrastructure, Documents
```

- Domain은 어떤 프로젝트도 참조하지 않음 (순수)
- Desktop은 Infrastructure/Documents를 직접 참조 (DI 등록, 파일 저장, COM 연동)
- Infrastructure/Documents는 서로 참조하지 않음 (문서 생성은 Application 인터페이스로 분리)

## 4. 레이어 매핑 (기존 → Windows)

| 기존 Python | Windows |
| --- | --- |
| `models.py` | Domain 엔티티 + Infrastructure EF 매핑 |
| `routers/*.py` | Application 서비스 (화면은 ViewModel이 직접 호출) |
| `serializers.py` | Application DTO |
| `document_dtos.py` / `document_builders.py` | Documents 프로젝트 DTO/빌더 |
| `hwpx_engine.py` / `hwpx_service.py` | Documents 프로젝트 HWPX 엔진 |
| `hwpx_pdf_renderer.py` | Documents 프로젝트 COM 렌더러 |
| `importer.py` / `xlsx_reader.py` | Documents(또는 Infrastructure) 이관 모듈 |
| `stats_service.py` 등 통계 5종 | Application 통계 서비스 |
| `app.js` / `app.html` | Desktop ViewModel + XAML 뷰 |
| `main.py` startup | App.xaml.cs 시작 로직 (마이그레이션 + 시드) |

## 5. MVVM 구조 제안

```text
View (XAML) ── DataContext ──→ ViewModel ──→ Application Service ──→ Repository ──→ DbContext
     │                            │
     └── Command/INotifyPropertyChanged (CommunityToolkit.Mvvm)
```

- **ViewModel**: `ObservableObject` 상속, `[ObservableProperty]`/`[RelayCommand]` 소스 생성기
- **네비게이션**: `MainViewModel` + `CurrentViewModel` 교체 방식 (간단한 뷰 전환). 프레임워크 도입(예: Prism)은 과할 수 있음 — 단일 영양사용 단순 앱이므로 **경량 네비게이션** 권장
- **대화상자**: `IDialogService` 인터페이스 (메뉴 선택기, 문서 미리보기, 확인 대화상자)
- **비동기**: `async/await` + `IAsyncRelayCommand` (DB/파일/COM 작업)

## 6. 데이터 계층 설계

### 6-1. EF Core 매핑 핵심
- 스냅샷 컬럼(`menu_name_snapshot` 등)은 일반 컬럼으로 매핑
- JSON 컬럼(`document_previews.payload`, `import_jobs.summary` 등)은 `JsonSerializer` 변환 (EF Core 8+ `ToJson()` 또는 값 변환기)
- unique 제약: `(service_date, meal_type)`, `(menu_id, version)`, `(menu_id, composition_key)`, `(service_date, ingredient_id)` — ingredient_id NULL 허용 주의 (SQLite unique는 NULL 중복 허용, 기존과 동일)
- `meal_type`은 문자열 코드 유지 (기존 데이터 호환) + `MealTypeSetting`과 조인

### 6-2. 마이그레이션/시드
- 시작 시 `db.Database.Migrate()` + 기본 데이터 시드 (admin 계정, 중식/석식 기본값)
- 기존 PostgreSQL/SQLite 데이터 이관 도구: `cafeteria.db`(SQLite) 또는 pg_dump → XLSX → 이관 파이프라인 재사용

### 6-3. 백업
- SQLite: 파일 복사 + SHA-256 (기존 pg_dump 대체). WAL 모드면 checkpoint 후 복사
- 백업/아카이브 목록은 기존 `backup_records`/`data_archives` 테이블 구조 유지

## 7. 문서 시스템 설계

### 7-1. HWPX 엔진 C# 이식
- `HwpxPackage`: ZIP 파일 dict 로드/저장 (`ZipArchive`)
- `HwpxTemplateEngine`: `XDocument` 기반 플레이스홀더 치환, `linesegarray` 제거, run 삽입, section 복제, content.hpf manifest/spine 재생성
- 반복 페이지: XML 주석 마커(`CAFETERIA_REPEAT_PAGE_START/END`) 파싱 → 블록 deepcopy → 로컬 치환 → `pageBreak="1"`
- 검증: 필수 파일, XML 파싱, manifest/spine, 필수 플레이스홀더, `{{` 잔존 검사

### 7-2. PDF 변환
- `HancomComPdfRenderer`: `Type.GetTypeFromProgID("HWPFrame.HwpObject")` → `Activator.CreateInstance` → COM 인터페이스 호출
- WPF 프로세스 내 COM 사용 시 STA 스레드 필요 (WPF는 기본 STA)
- 한컴오피스 미설치 환경: 명확한 오류 메시지 + HWPX 다운로드만 제공 (기존 UX 유지)

### 7-3. 문서 미리보기
- WPF에서 PDF 미리보기: `WebView2` + PDF.js 또는 외부 뷰어. HWPX는 생성 후 저장 경로 제공
- 기존 HTML 미리보기 템플릿은 WPF용으로 재설계 (또는 WebView2로 HTML 재사용 가능)

## 8. 통계/대시보드 설계
- 기존 통계 로직(식수 편차, 메뉴 반복, 미사용, 재료 사용량, 운영 완료율)은 Application 서비스로 이식
- 차트: WPF 차트 라이브러리(LiveCharts2 등) 또는 간단한 커스텀 렌더링
- 대시보드 데이터는 기존 `operations_dashboard` 조합 구조 유지

## 9. 보안
- 비밀번호: 기존 PBKDF2-SHA256 포맷 호환 해셔 (`pbkdf2_sha256$240000$salt$digest`)
- 로컬 단일 사용자 앱이므로 세션/쿠키 불필요 — 로그인 화면 + 앱 잠금 수준으로 단순화
- 감사 로그: 기존 `audit_logs` 구조 유지 (사용자/백업/이관/중요 변경)

## 10. 리스크 요약
- **HWPX 엔진 이식이 최대 리스크** — XML 구조/네임스페이스/반복 페이지 로직의 정확한 재현 필요. 기존 템플릿 3종으로 회귀 테스트 필수
- **한컴오피스 COM** — 설치 환경 의존. WPF 프로세스 내 COM 안정성 검증 필요
- **기존 데이터 이관** — PostgreSQL → SQLite 이관 경로 설계 필요 (XLSX 경유 또는 직접)
- **JSON 컬럼** — EF Core 매핑 시 기존 데이터 호환 확인 필요
