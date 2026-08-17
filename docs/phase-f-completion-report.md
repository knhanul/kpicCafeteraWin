# Phase F — 운영 대시보드 및 통계 화면 UI/UX 리디자인 완료 보고서

## 1. 데이터 가용성 분석

### 기존 서비스 및 DTO 구조
- **DashboardService**: `MealStatisticsService`, `MenuStatisticsService`, `OperationsStatisticsService` 결과를 집계하여 `DashboardDto` 반환
- **MealStatisticsService**: 식수 통계(요일별 평균, 월별 추세, 이상치, 백데이터)
- **MenuStatisticsService**: 메뉴 통계(상위 메뉴, 반복, 미사용, 신규, 상세 드릴다운)
- **IngredientStatisticsService**: 식재료 통계(상위 식재료, 미사용, 신규, 상세 드릴다운)
- **OperationsStatisticsService**: 운영 기록 통계(완료율, 누락, 지연 입력, 보존식 분석)

### 데이터 소스
- `IStatisticsRepository`를 통해 DB에서 필요 컬럼만 프로젝션(`MealServiceRow`, `ActualHistoryRow`, `MenuUsageRow` 등)
- GroupBy, Sum, Count 기반 최적화 쿼리 — 메모리 로드 최소화
- 모든 KPI 데이터가 기존 서비스에서 이미 계산되어 제공됨 — **추가 서비스 구현 불필요**

## 2. KPI 정의

### 대시보드 (4개 KPI)
| KPI | 정의 | 데이터 소스 | 계산 방법 |
|-----|------|------------|-----------|
| 운영일수 | 기간 내 배식 건수 | `DashboardDto.Kpis.OperatingDays` | 배식 레코드 Count |
| 고유 메뉴 수 | 메뉴 ID + 스냅샷명 기준 | `DashboardDto.Kpis.UniqueMenuCount` | DISTINCT Count |
| 중식 요약 | 실제/계획(편차율) | `DashboardDto.Kpis.Lunch` | Breakdown DTO |
| 석식 요약 | 실제/계획(편차율) | `DashboardDto.Kpis.Dinner` | Breakdown DTO |

### 식수 통계 (7개 KPI)
운영일수, 총 계획 식수, 총 실제 식수, 실제 입력률, 계획 대비 편차, 중식 요약, 석식 요약

### 메뉴 통계 (5개 KPI)
고유 메뉴 수, 총 사용 횟수, 신규 메뉴(기간 시작 전 사용 이력 없음), 반복 메뉴(14일 2회 또는 28일 3회), 미사용 메뉴(최근 90일)

### 식재료 통계 (4개 KPI)
고유 식재료 수, 총 사용 횟수, 신규 식재료, 미사용 식재료(최근 90일)

### 운영 기록 통계 (7개 KPI)
배식 건수, 실제 식수 입력률, 보존식 완료율, 식단표 출력률, 조리지시서 출력률, 중식 요약, 석식 요약

## 3. 이상징후 탐지 기준

| 유형 | 기준 | 출처 |
|------|------|------|
| 식수 이상치 | 계획 대비 편차 + 평소 중앙값 대비 편차 | `MealAnomalyDto` (Type, Level 포함) |
| 메뉴 반복 | 14일 내 2회 이상 또는 28일 내 3회 이상 | `MenuRepeatDto` |
| 재료 사용량 변화 | 직전 동일 기간 대비 ±25% 확인, ±40% 중요 | `IngredientChangeDto` |
| 기록 누락 | 배식 있으나 기록 없음 | `RecordGapDto` |
| 지연 입력 | 기록일 - 배식일 > 1일 | `LateInputDto` |

모든 기준은 사용자가 이해할 수 있는 단순 규칙 기반이며, UI에 기준 명시.

## 4. UI 구조

### 공통 패턴
- **Toolbar**: 페이지 타이틀 + `StatisticsFilterBar`(기간 프리셋, 시작/종료일, 식사 구분)
- **KPI Cards**: `KpiCard` 컨트롤로 상단 배치 (최대 7개)
- **Section Cards**: `StatisticsSection` 스타일로 카드형 섹션 (추세, 랭킹, 이상치, 백데이터)
- **Drill-down**: 메뉴/식재료 통계는 우측 패널에서 상세 정보 제공 (월별 사용, 최근 이력, 함께 사용된 항목, 사용 이력 DataGrid)
- **Backdata**: 모든 통계 화면 하단에 검색 가능한 DataGrid로 원본 데이터 표시
- **Loading State**: `IsBusy` 바인딩으로 로딩 오버레이 + ProgressBar 표시
- **Empty State**: `EmptyStateText` 스타일로 데이터 없음 안내

### 화면별 레이아웃
1. **대시보드**: 2열 레이아웃 (좌: 추세+랭킹, 우: 이상징후 4종)
2. **식수 통계**: KPI → 요일별/월별 → 이상치 → 백데이터
3. **메뉴 통계**: KPI → 2열 (좌: 상위/반복/미사용/백데이터, 우: 상세 패널)
4. **식재료 통계**: KPI → 2열 (좌: 상위/미사용/백데이터, 우: 상세 패널)
5. **운영 기록 통계**: KPI → 월별 추세/이상징후 → 보존식 분석 → 백데이터

## 5. 차트 기술

- **기존 `BarChart` 컨트롤 유지**: 가로 막대 차트, 외부 의존성 없음
- 색상을 디자인 시스템에 맞게 업데이트: Accent(#2563EB), TextDisabled(#9DA3AB), Success(#16A34A), Warning(#D97706)
- **새 차트 라이브러리 도입 없음** — 외부 의존성 금지 원칙 준수

## 6. 드릴다운 설계

- 메뉴/식재료 통계: DataGrid 행에서 "보기" 버튼 → 우측 패널에 상세 정보 표시
- 상세 정보: 월별 사용, 최근 20건 이력, 함께 사용된 항목, 전체 사용 이력 DataGrid
- 모든 통계 화면: 하단 백데이터 DataGrid에 검색 기능(날짜, 명, 구분)으로 원본 데이터 접근
- 대시보드 → 통계 화면으로의 네비게이션은 좌측 메뉴를 통해 제공

## 7. 디자인 시스템 적용

### 변경된 파일
- `Controls/KpiCard.xaml`: 디자인 시스템 브러시 적용 (Surface, BorderSubtle, TextPrimary/Secondary)
- `Controls/BarChart.xaml`: 디자인 시스템 브러시 적용, 레이블/값 영역 개선
- `Controls/BarChart.xaml.cs`: 기본 색상 #2563EB(Accent)로 변경
- `Styles/Workspace.xaml`: 통계 전용 스타일 11종 추가 (StatisticsSection, StatisticsSectionTitle, AnomalyItem, AnomalyText, EmptyStateText, StatisticsToolbar, PeriodDisplayText, LoadingOverlay, RankingItem/Label/Value, DetailPanel)
- `MainWindow.xaml`: 네비게이션 버튼에 SubtleButton 스타일 적용, 디자인 시스템 브러시 사용

### 색상 매핑
| 용도 | 기존 | 변경 |
|------|------|------|
| 계획 막대 | #9AA7B8 | #9DA3AB (TextDisabled) |
| 실제 막대 | #4C7BD9 | #2563EB (Accent) |
| 보존식 | #5BA87A | #16A34A (Success) |
| 조리지시서 | #D98E4C | #D97706 (Warning) |
| 이상징후 | #B3541E | WarningBrush (#D97706) |

## 8. 변경 파일 목록

| 파일 | 변경 내용 |
|------|-----------|
| `Controls/KpiCard.xaml` | 디자인 시스템 브러시 적용 |
| `Controls/BarChart.xaml` | 디자인 시스템 브러시, 개선된 레이아웃 |
| `Controls/BarChart.xaml.cs` | 기본 색상 Accent로 변경 |
| `Styles/Workspace.xaml` | 통계 전용 스타일 11종 추가 |
| `MainWindow.xaml` | 네비게이션 디자인 시스템 적용 |
| `Views/Statistics/DashboardView.xaml` | 전면 리디자인 |
| `Views/Statistics/MealStatisticsView.xaml` | 전면 리디자인 |
| `Views/Statistics/MenuStatisticsView.xaml` | 전면 리디자인 |
| `Views/Statistics/IngredientStatisticsView.xaml` | 전면 리디자인 |
| `Views/Statistics/OperationsStatisticsView.xaml` | 전면 리디자인 |
| `Views/Statistics/StatisticsFilterBar.xaml` | 디자인 시스템 스타일 적용 |
| `ViewModels/Statistics/DashboardViewModel.cs` | 차트 색상 업데이트 |
| `ViewModels/Statistics/MealStatisticsViewModel.cs` | 차트 색상 업데이트 |
| `ViewModels/Statistics/OperationsStatisticsViewModel.cs` | 차트 색상 업데이트 |

## 9. 비즈니스 로직 변경

- **없음** — 모든 ViewModel 바인딩 프로퍼티, 서비스 호출, DTO 구조 유지
- 색상 hex 값만 디자인 시스템 토큰으로 교체

## 10. 테스트 결과

- **빌드**: 0 오류, 0 경고
- **단위 테스트**: 295개 통과, 0 실패

## 11. 금지 사항 준수

- 외부 인터넷 의존성/CDN: 사용 안 함
- WebView2: 사용 안 함
- AI/머신러닝: 사용 안 함
- 새 UI 프레임워크: 사용 안 함 (기존 WPF 컨트롤 + 디자인 시스템만 사용)
- 비즈니스 로직/데이터 구조 변경: 없음
- 범위 외 기능 추가: 없음

## 12. 향후 개선 사항

- 대시보드에서 통계 화면으로 바로 이동하는 바로가기 버튼 (필요시)
- 대시보드 KPI 카드 클릭 시 해당 통계 화면으로 네비게이션 (필요시)
- 백데이터 Excel 내보내기 기능 (필요시)
- DPI 스케일링 추가 검증 (고해상도 모니터 환경)
