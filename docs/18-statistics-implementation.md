# 18. Statistics(통계·운영 대시보드) 구현 기록

> 7단계 통계/운영 대시보드 실제 구현 내용.
> 작성일: 2026-08-16

## 1. 범위

**포함**
- 식수 통계 (계획/실제/편차/평소 중앙값/이상치/추세/백데이터)
- 메뉴 통계 (사용 빈도/반복/미사용/신규/상세/백데이터)
- 식재료 통계 (사용 빈도/사용량/미사용/신규/상세/백데이터)
- 운영 기록 통계 (실제 입력/보존식/식단표/조리지시서 완료율/누락/지연/백데이터)
- 운영 대시보드 (KPI/월별 추세/이상징후/요약)

**제외 (의도적)**
- 발주 통계, Excel/PDF 리포트, AI 분석, 복합 BI(피벗/드릴다운 계층) 등
- 기존 Python `statistics_service.py`/`menu_statistics.py`/`ingredient_statistics.py`/`operations_statistics.py`/`dashboard_service.py`의 **계산 로직은 이식**하되, Windows 구조에 맞게 서비스/리포지토리/ViewModel로 재구성

## 2. 아키텍처

```text
Application (서비스 + DTO)
├─ StatisticsPeriod        기간 프리셋 계산 (이번 달/3개월/6개월/12개월/올해/직접 선택)
├─ StatisticsWeekday       요일 인덱스 (Python weekday와 동일: 월=0 … 일=6)
├─ StatisticsDtos          통계 DTO 레코드 (식수/메뉴/식재료/운영/대시보드)
├─ StatisticsExceptions    StatisticsException / NoStatisticsDataException
├─ MealStatisticsService / MenuStatisticsService / IngredientStatisticsService
├─ OperationsStatisticsService / DashboardService
└─ Abstractions.Repositories
   ├─ IStatisticsRepository        읽기 전용 통계 쿼리 계약
   └─ IStatisticsRepositoryFactory DbContext 수명 관리

Infrastructure
└─ StatisticsRepository / StatisticsRepositoryFactory
   └─ EF Core AsNoTracking + Projection (전체 로드 후 메모리 계산 없음)

Desktop (MVVM)
├─ ViewModels.Statistics
│  ├─ StatisticsViewModelBase      기간/식사유형/로딩/오류 공통
│  ├─ StatisticsPeriodViewModel    기간 선택 (프리셋 + 직접 선택)
│  ├─ DashboardViewModel / MealStatisticsViewModel / MenuStatisticsViewModel
│  ├─ IngredientStatisticsViewModel / OperationsStatisticsViewModel
│  └─ StatisticsFormat / BarChartItem
├─ Views.Statistics (5개 화면 + StatisticsFilterBar)
└─ Controls (KpiCard / BarChart — 외부 차트 라이브러리 없음)
```

## 3. 데이터 흐름

1. ViewModel이 `StatisticsPeriod.Resolve()`로 (시작일, 종료일) 결정
2. `IStatisticsRepositoryFactory.Create()`로 짧은 수명 DbContext 생성
3. `IStatisticsRepository`가 EF Projection으로 필요한 집계만 조회 (`AsNoTracking`)
4. 서비스가 Python 로직과 동일한 계산 수행 (중앙값/편차/이상치/반복/미사용/완료율)
5. DTO → ViewModel 바인딩 → KPI 카드/차트/DataGrid 표시

## 4. 계산 규칙 (Python 이식)

### 식수 (MealStatisticsService)
- `InputRate = ActualInputCount / ServiceCount`
- `DeviationRate = (ActualSum - PlannedSum) / PlannedSum`
- 평소 중앙값: 같은 요일·같은 식사유형의 최근 10회 실제 식수 중앙값
- 이상치: `|편차율| ≥ 20%`(중요) 또는 `≥ 10%`(확인), 평소 중앙값 대비 `|편차| ≥ 30%`(중요)/`≥ 15%`(확인)
- 비교 데이터 부족(평소 5회 미만) 시 `InsufficientComparison`

### 메뉴 (MenuStatisticsService)
- 고유 메뉴: `MenuId` 또는 스냅샷명 기준
- 반복: 14일 2회 이상 또는 28일 3회 이상
- 미사용: `unusedDays`(기본 90) 동안 사용 이력 없음
- 신규: 기간 시작 전 사용 이력 없음
- 상세: 월별 사용/최근 이력(20건)/함께 사용된 메뉴/사용 이력

### 식재료 (IngredientStatisticsService)
- 고유 식재료: `IngredientId` 또는 스냅샷명 기준
- 사용량: `QuantityTotal × (ActualCount/PlannedCount)` (실제 미입력 시 계획 기준)
- `analysis_excluded` 재료는 통계에서 제외
- 미사용/신규/상세는 메뉴 통계와 동일 규칙

### 운영 기록 (OperationsStatisticsService)
- 실제 입력률/보존식 완료율/식단표 출력률/조리지시서 출력률
- 기록 누락: 실제 입력/보존식/식단표/조리지시서 중 미기록
- 지연 입력: 기록 시각 - 배식일 > 1일
- 보존식 분석: 수거/폐기 건수·비율, 담당자별, 온도 기록

### 대시보드 (DashboardService)
- KPI: 운영일수/고유 메뉴 수/중식·석식 요약
- 월별 추세: end 기준 12개월 전부터 데이터가 있는 월만
- 이상징후: 식수 이상/메뉴 반복/재료군 변화(전기 대비 ±30% 중요, ±15% 확인)/기록 누락
- 요약: 메뉴 사용 상위 5/반복 메뉴(기간 + 직전 4주)/재료군 사용량(kg 환산)/업무 기록 현황
- 재료군 kg 환산: g→kg(÷1000), ml→L(÷1000), kgFactor 있는 단위는 계수 곱, 환산 불가 단위는 kg 미포함

## 5. 요일 매핑

- Python `date.weekday()`: 월=0 … 일=6
- C# `DateTime.DayOfWeek`: 일=0 … 토=6
- `StatisticsWeekday` 헬퍼로 C# 값을 Python 인덱스로 변환 후 사용 (테스트로 검증)

## 6. WPF 화면

```text
MainWindow
├─ 좌측 네비게이션 "통계" 그룹
│  ├─ 운영 대시보드 / 식수 통계 / 메뉴 통계 / 식재료 통계 / 운영 기록 통계
└─ ContentControl

StatisticsFilterBar (공용)
├─ 기간 프리셋 ComboBox + 시작일/종료일 DatePicker(직접 선택 시 활성화)
└─ 식사유형 ComboBox (전체/중식/석식)

DashboardView: KPI 카드 4 + 월별 추세(계획/실제) + 메뉴 사용/반복/재료군/업무 기록 + 이상징후 4종
MealStatisticsView: KPI 7 + 요일별 평균 + 월별 추세 + 식수 이상치 + 백데이터(검색 필터)
MenuStatisticsView: KPI 5 + 상위 메뉴(상세 버튼) + 반복/미사용 + 백데이터 + 우측 상세 패널
IngredientStatisticsView: KPI 4 + 상위 식재료(상세 버튼) + 미사용 + 백데이터 + 우측 상세 패널
OperationsStatisticsView: KPI 7 + 월별 완료율 추세 + 기록 누락/지연 + 보존식 분석 + 백데이터
```

- `KpiCard`: 라벨/값/보조 텍스트 DependencyProperty 기반 카드
- `BarChart`: ItemsSource → 최대값 대비 백분율 가로 막대 (INotifyPropertyChanged로 갱신)
- 모든 로드는 `AsyncRelayCommand` + `IsBusy` 가드, 오류 시 `IMessageService.ShowError`

## 7. 테스트

| 테스트 클래스 | 수 | 검증 |
| --- | --- | --- |
| MealStatisticsServiceTests | 14 | 중앙값/편차율/이상치(중요·확인)/비교 부족/필터/백데이터/요일 매핑 |
| MenuStatisticsServiceTests | 13 | 그룹핑/신규/반복(14일·28일)/미사용/상세/백데이터/FK 연결 |
| IngredientStatisticsServiceTests | 13 | 사용량 계산/그룹핑/신규/미사용/상세/analysis_excluded/요일 매핑 |
| OperationsStatisticsServiceTests | 9 | 완료율 4종/누락/지연/보존식 분석/백데이터/요일 매핑 |
| DashboardServiceTests | 10 | KPI/추세/반복 메뉴(직전 4주)/재료군 kg 환산/이상징후/백데이터 |
| StatisticsPerformanceTests | 1 | 1년치 평일 중식·석식(약 500건) + 메뉴/재료 스냅샷 → 30초 내 계산 |

- **60개 신규 테스트 전부 통과** (전체 291개)
- 실제 SQLite in-memory 엔진 사용, EF InMemory Provider 미사용
- `StatisticsTestHarness`/`StatisticsFixture`로 배식/메뉴/재료/실제/보존식 기록 생성
- FK 위반 방지를 위해 navigation property로 연결 후 `Save()` (ID 캡처는 Save 후)

## 8. DI 등록 (App.xaml.cs)

```text
IStatisticsRepositoryFactory → StatisticsRepositoryFactory
MealStatisticsService / MenuStatisticsService / IngredientStatisticsService
OperationsStatisticsService / DashboardService
DashboardViewModel / MealStatisticsViewModel / MenuStatisticsViewModel
IngredientStatisticsViewModel / OperationsStatisticsViewModel
DashboardView / MealStatisticsView / MenuStatisticsView
IngredientStatisticsView / OperationsStatisticsView
```

## 9. 미확인 사항

- **M1**: 실제 UI 클릭 시나리오(기간 변경/상세 패널/백데이터 검색/차트 갱신)는 자동 클릭 불가 — `docs/19` 수동 체크리스트 참조
- **M2**: 차트는 가로 막대(ItemsControl)로 구현 — 툴팁/범례/축 눈금은 미지원 (의도)
- **M3**: 백데이터는 기간 내 전체 행 표시 (페이지네이션 미지원, 검색 필터 제공)
- **M4**: `unusedDays`는 화면에서 90일 고정 (Python 기본값과 동일)
