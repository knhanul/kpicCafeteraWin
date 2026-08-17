# 데이터 관리 (Data Management) 구현

## 개요

기존 Python 백엔드(`admin.py`, `routers/setup.py`, `importer.py`, `xlsx_reader.py`)의 **이관(Import) / 백업(Backup) / 복구(Restore) / Excel 아카이브(Archive)** 기능을 KpicCafeteria Windows 애플리케이션으로 이관한다.

## 구성 요소

### 1. XLSX 이관 (Import)

- `IImportService` / `ImportService`
  - `PreviewAsync(file)` — 필수 시트 존재/누락 및 행 수 검증
  - `ApplyAsync(file, mode)` — Replace / Merge 모드로 DB 반영
- `MigrationImporter`
  - 7개 시트(`01_배식설정` ~ `07_식단재료_이관`) 파싱
  - `CompositionKey`로 재료 구성 그룹핑
  - `MealServiceMenu` / `MealServiceMenuIngredient` 스냅샷 생성
- `XlsxWorkbookReader`, `XlsxCellParser`
  - ClosedXML 기반 셀/행 추출, 날짜/시간/숫자/불리언 정제

### 2. 시스템 백업

- `IBackupService` / `BackupService`
  - 수동, 자동, 복구 직전 백업 지원
  - SQLite `wal_checkpoint(TRUNCATE)` 후 `BackupDatabase`로 안전한 DB 복사
  - ZIP 패키지(`*.kpicbackup`) + `manifest.json` + 템플릿 포함
  - `BackupRecord` 엔티티로 히스토리 관리
- `BackupManifest`
  - 백업 버전, 생성 시각, 앱 버전, DB 마이그레이션 버전, 파일 체크섬 포함

### 3. 시스템 복구

- `IRestoreService` / `RestoreService`
  - ZIP + manifest 검증
  - DB `PRAGMA integrity_check` 및 checksum 검증
  - 복구 직전 자동 사전 백업 (`CreatePreRestoreBackupAsync`)
  - 원자적 파일 교체 + `SqliteConnection.ClearAllPools()`
  - 복구 후 `db.Database.MigrateAsync()`로 마이그레이션 보정
  - 복구 성공 시 애플리케이션 재시작

### 4. Excel 데이터 아카이브

- `ExcelArchiveExporter`
  - 기존 8개 시트 + `식단재료`, `발주기록`, `발주그룹` 시트 추가
- `ExcelExportService`
  - 기간별 조회 + `IOrderRepository` 연계
  - `SaveToArchiveAsync`로 아카이브 디렉터리에 저장

### 5. WPF 데이터 관리 화면

- `DataManagementViewModel` / `DataManagementView`
  - XLSX 파일 선택 / Preview / Replace / Merge
  - 백업 목록 / 수동 생성 / 선택 복구
  - 기간별 Excel 아카이브 생성
- `MainWindow`에 "데이터 관리" 네비게이션 추가
- `App.xaml.cs` DI 등록

## 참고

- 복구 시 Replace 모드는 `BackupService`를 통해 사전 백업 후 원자적으로 교체된다.
- 자동 백업은 24시간 간격으로 생성되며 30개 보관 후 정리된다.
