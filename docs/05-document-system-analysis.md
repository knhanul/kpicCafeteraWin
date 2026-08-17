# 05. 문서 시스템 분석 (Excel / HWPX / PDF)

## 1. 문서 출력 개요

기존 시스템의 문서 출력은 **HWPX를 정본으로 생성하고, 그 결과를 PDF 미리보기/다운로드에 재사용**하는 구조다.

```text
DB / ORM
  └─ document_builders.py   (ORM → 문서 DTO)
       └─ document_dtos.py  (pydantic DTO, extra="forbid")
            └─ document_hwpx.py
                 ├─ hwpx_engine.py        (HWPX 템플릿 엔진)
                 └─ hwpx_pdf_renderer.py  (한컴오피스 COM → PDF)
```

원칙:
- DB → DTO → HWPX 생성 → PDF 변환 순서 유지
- HWPX를 건너뛰는 직접 HTML/PDF 출력은 사용하지 않음
- PDF 미리보기와 HWPX 다운로드는 같은 데이터 소스에서 생성
- HWPX 패키지를 처음부터 생성하지 않고 **등록된 템플릿의 표·행 높이·셀 병합·글꼴을 보존**하며 플레이스홀더만 치환

## 2. 문서 유형 3종

| 문서 유형 | 코드 | HTML 템플릿 | HWPX 렌더러 | 페이지 용량 |
| --- | --- | --- | --- | --- |
| 식단표 | `MEAL_PLAN` | `templates/documents/meal_plan.html` | `MealPlanRenderer` | 2주/페이지 |
| 조리지시서 | `COOKING_INSTRUCTION` | `templates/documents/cooking_instruction.html` | `CookingInstructionRenderer` | 1일(중식+석식)/페이지 |
| 보존식 기록지 | `PRESERVATION_RECORD` | `templates/documents/preservation.html` | `PreservedFoodRenderer` | 3식/페이지 |

## 3. 데이터 흐름

### 3-1. DTO 생성 (document_builders.py)
- `MealPlanDocumentBuilder`: 기간 내 배식을 월~금 주 단위로 그룹핑. `MealPlanDocumentDTO { period, weeks[{days[{date, lunch, dinner}]}] }`. 중식/석식 블록에 `meal_count`, `service_time`, `concept_title`, `menus[]`(메뉴명 스냅샷 목록).
- `CookingInstructionDocumentBuilder`: 일자별 `CookingInstructionDayDTO { date, lunch, dinner }`. 메뉴에 `ingredients[]`(이름/수량/100인수량/단위/비고), `instruction`, `note`.
- `PreservationRecordDocumentBuilder`: 배식별 `PreservationRecordBlockDTO` (날짜/식사명/메뉴/채수시각/관리자/온도/폐기일/채수자). (날짜, 중식→석식) 정렬.

DTO 규칙:
- 누락값은 예외 대신 빈 값/None으로 보존
- 메뉴/재료 입력 순서 유지
- `extra="forbid"` — 정의되지 않은 필드 거부

### 3-2. payload 변환 (document_hwpx.py)
- DTO → 렌더러용 dict payload (`_dto_to_payload`)
- 식단표 payload: `{title, period_label, weeks[{start, end, week_label, days[{date, date_label, weekday, services{LUNCH, DINNER}}]}]}`
- 조리지시서 payload: `{title, period_label, days[{date, date_label, weekday, services[{meal_type, meal_name, planned_count, service_time, menus[]}]}]}`
- 보존식 payload: `{title, period_label, records[{date_label, weekday, meal_name, sample_datetime, manager_name, menu_items, freezer_temperature, discard_datetime, collector_name, collection_time}]}`

### 3-3. HWPX 렌더링 (hwpx_engine.py, 약 1,250줄)

**HwpxPackage**: HWPX(ZIP)를 파일 dict로 로드/저장. 검증: 필수 파일(`mimetype`, `Contents/content.hpf`, `Contents/header.xml`, `version.xml`, `META-INF/container.xml`, `Contents/section*.xml`), XML 파싱, manifest/spine 참조 일치, ZIP Slip 방지, 플레이스홀더 검출.

**HwpxTemplateEngine**:
- `setField` / `setMultilineField`: 단일 문단의 `t` 노드 텍스트에서 `{{TOKEN}}` 치환. 치환 후 `linesegarray` 제거.
- `setMultilineFieldWithNoteColor`: 비고 텍스트를 별도 run + 파란색 charPr로 삽입 (조리지시서).
- `ensureCookingStyles`: header.xml에 LEFT 정렬 paraPr(100)와 파란색 charPr(100) 추가 (없으면).
- `applyRepeatPages`: 반복 페이지 처리 (아래 5장 참조).
- `cloneRow` / `cloneBlock` / `removeBlock`: 마커 기반 행/블록 복제·삭제 (현재 렌더러에서 미사용, API만 존재).
- `ensureSectionCount`: section XML 수 조정 + content.hpf manifest/spine 재생성 + header.xml `secCnt` 갱신.

**렌더러별 처리**:

| 렌더러 | 반복 페이지 | 비반복 경로 |
| --- | --- | --- |
| `MealPlanRenderer` | 2주 단위 chunk → `applyRepeatPages` | `W{1,2}_D{1..5}_DATE/LUNCH_MENU/DINNER_MENU` + `PERIOD_TITLE` 치환. `ORIGIN_INFO`/`NOTICE`/`W1_LUNCH_TIME_INFO`/`W2_LUNCH_TIME_INFO`/`DINNER_TIME_INFO`는 빈 값 처리 |
| `CookingInstructionRenderer` | 1일 단위 chunk → `applyRepeatPages` | 일자별 section 복제, `DATE_LABEL` + `LUNCH/DINNER_MENU_{1..7}` + `INGREDIENTS_{1..7}` + `_TITLE` 치환. 7슬롯 초과 시 마지막 슬롯 병합 |
| `PreservedFoodRenderer` | 3식 단위 chunk → `applyRepeatPages` | section = ceil(식수/3), `B{1..3}_DATE_LABEL/SAMPLE_DATETIME/MANAGER/MENU_LIST/FREEZER_TEMP/DISCARD_DATETIME/COLLECTOR/COLLECTION_TIME` 치환 |

**최종 검증** (`render_document`):
- `validatePackage(allow_remaining_placeholders=False)` — `{{...}}` 잔존 시 실패
- `save(validate=False)` — ZIP 재검증(필수 파일, XML 파싱)

### 3-4. PDF 변환 (hwpx_pdf_renderer.py)
- `HancomComPdfRenderer`: 임시 디렉터리에 HWPX bytes 저장 → **별도 Python 서브프로세스**에서 `win32com.client.Dispatch("HWPFrame.HwpObject")` 실행 → `RegisterModule("FilePathCheckDLL", "FilePathCheckerModule")` → `Open(src, "HWPX", "forceopen:true;versionwarning:false;")` (5회 재시도) → `SaveAs(dst, "PDF", "")` (5회 재시도) → `%PDF` 매직 바이트 확인 → 임시 파일 정리.
- `default_pdf_renderer()`가 기본 렌더러 반환 (교체 가능한 Protocol 구조).

## 4. 템플릿 시스템

### 4-1. 템플릿 저장
- 업로드된 HWPX 템플릿은 `storage/templates/{document_type}/`에 저장
- `DocumentTemplate` 테이블에 메타데이터(버전, SHA-256, 활성 여부, 검증 결과, 플레이스홀더 요약) 기록
- 활성 템플릿은 `active_template(db, document_type)`으로 조회 (유형당 1개, version 내림차순)

### 4-2. 템플릿 검증 (validate_hwpx)
1. 파일 크기 ≥ 1KB
2. ZIP 구조 + ZIP Slip 경로 검사
3. 필수 파일 존재 (`mimetype`, `Contents/content.hpf`, `Contents/header.xml`, `version.xml`, `META-INF/container.xml`, `Contents/section*.xml`)
4. 모든 XML 파싱 가능
5. manifest/spine 참조 일치
6. **문서 유형별 필수 플레이스홀더** 존재:
   - `PRESERVATION_RECORD`: `R1_DATE`, `R1_MENU`, `R2_DATE`, `R2_MENU`, `R3_DATE`, `R3_MENU` (구버전 hwpx_service 기준)
   - `COOKING_INSTRUCTION`: `DATE`, `LUNCH_BODY`, `DINNER_BODY` (구버전 기준)
   - `MEAL_PLAN`: `D1_DATE`, `D1_LUNCH`, `D1_DINNER`, `D10_DATE`, `D10_LUNCH`, `D10_DINNER` (구버전 기준)
   - 신버전 `hwpx_engine.REQUIRED_PLACEHOLDERS`는 더 상세한 세트 (W1_D1_DATE... 등 30~60개)

> 참고: `hwpx_service.py`에는 구버전 구현과 신버전(`hwpx_engine` 위임) 구현이 공존한다. 파일 하단에서 `HwpxTemplateError`/`validate_hwpx`/`render_hwpx`를 신버전으로 재정의한다.

### 4-3. 반복 페이지 처리 (핵심)

**템플릿 마커** (`TEMPLATE_PAGE_MODEL.md`):
```xml
<!-- CAFETERIA_REPEAT_PAGE_START ... -->
... page block (1페이지 분량) ...
<!-- CAFETERIA_REPEAT_PAGE_END -->
```

**처리 순서** (`applyRepeatPages`):
1. `section0.xml`에서 주석 마커 사이의 최상위 노드 블록을 템플릿으로 추출
2. 원본 블록 제거
3. 페이지 수만큼 블록 deepcopy → **복제본 내부에서만(Local Scope) 플레이스홀더 치환** → append
4. 두 번째 페이지부터 첫 번째 최상위 `hp:p`에 `pageBreak="1"` 설정
5. 문서 전체 replaceAll 금지 (페이지 번호 없는 로컬 플레이스홀더 재사용)

**페이지 수 규칙** (`template-page-config.json`):
- 식단표: `ceil(주 수 / 2)` — 2주 단위, 마지막 페이지 홀수 주면 W2 빈 값
- 조리지시서: 선택 일자 수 — 1일(중식+석식)
- 보존식 기록지: `ceil(식수 / 3)` — 3식 단위, 남는 슬롯 빈 값

**반복 페이지가 적용되는 조건**: `REPEAT_PAGE_CONFIG_BY_DOCUMENT_TYPE`에 설정이 있고 `applyRepeatPages`가 마커를 찾으면 사용. 없으면 기존 비반복 경로(전체 치환)로 폴백.

### 4-4. 문서별 플레이스홀더 (신버전 REQUIRED_PLACEHOLDERS)

**식단표** (30개): `PERIOD_TITLE`, `W1_D1_DATE`~`W2_D5_DINNER_MENU` (주 5일 × 중식/석식)

**조리지시서** (30개): `DATE_LABEL`, `LUNCH_MENU_1`~`LUNCH_MENU_7`, `LUNCH_INGREDIENTS_1`~`7`, `DINNER_MENU_1`~`7`, `DINNER_INGREDIENTS_1`~`7`

**보존식 기록지** (25개): `PERIOD_TITLE`, `B1_DATE_LABEL`~`B3_COLLECTION_TIME` (3슬롯 × 8필드)

### 4-5. 템플릿 파일 (docs/template/)
- `식단표_반복페이지템플릿.hwpx` (46,767 bytes, sha256 `68a9bd53...`)
- `조리지시서_반복페이지템플릿.hwpx` (31,979 bytes, sha256 `228083c1...`)
- `보존식기록지_반복페이지템플릿.hwpx` (35,495 bytes, sha256 `004e0033...`)
- `template-page-config.json` — 페이지 용량/로컬 슬롯/페이지 규칙 설정
- 참조 PDF 샘플: `docs/reference/식단표.pdf`, `주간조리지시서.pdf`, `보존식기록지.pdf`

## 5. Excel (아카이브)

### 5-1. Excel 데이터 아카이브 (admin.py `_build_excel`)
openpyxl로 9개 시트 생성:
1. `식단기록`: 날짜/식사구분/계획인원/서비스시간/콘셉트/메뉴목록(쉼표 결합)/비고
2. `조리지시서`: 날짜/식사구분/메뉴명/조리지시/조리비고
3. `보존식기록`: 날짜/식사구분/채수시간/채수자/냉동고온도/관리자/폐기시간/비고
4. `실제식수`: 날짜/식사구분/실제인원/비고/기록시간
5. `메뉴기준정보`: ID/메뉴명/역할/사용여부/검토상태
6. `재료기준정보`: ID/재료명/통계분석군/기본단위/kg환산계수/사용여부
7. `레시피`: 레시피ID/메뉴명/레시피명/버전/재료명/100인분량/단위
8. `식사유형설정`: 코드/이름/기본인원/기본시간/정렬순서/사용여부
9. `사용자목록`: ID/사용자ID/사용자명/권한/사용여부/최근로그인 (비밀번호 제외)

- 열 너비 자동 조정, freeze panes A2
- 24시간 보존 후 만료 처리 (`expires_at`), cleanup API로 정리
- **백업(복구용)이 아니라 장기보관용 조회 자료**임을 명시

### 5-2. XLSX 읽기 (이관)
- `xlsx_reader.py::SimpleXlsxReader` — 표준 라이브러리만 사용 (ZIP + XML 파싱)
- sharedStrings, inlineStr, 숫자/날짜 처리, Excel 1900 날짜 시스템 변환
- openpyxl은 아카이브 **쓰기**에만 사용

## 6. 테스트 상태

| 테스트 파일 | 검증 내용 | 실행 환경 |
| --- | --- | --- |
| `test_document_builders.py` | 3개 빌더의 DTO 생성 (주 그룹핑, 메뉴 순서, null 보존, 정렬) | SQLite in-memory |
| `test_document_hwpx.py` | DTO→HWPX 다운로드 (파일명, ZIP 무결성, 플레이스홀더 제거, 출력 시각 기록, section 복제) | SQLite + 가짜 템플릿 |
| `test_hwpx_engine.py` | 템플릿 검증, 렌더링 round-trip (ZIP 무결성, `{{` 잔존 없음, section 확장) | 가짜 템플릿 |
| `test_hwpx_output_system.py` | 실제 데이터셋(전체 주/희소 데이터/대량 재료/특수문자) HWPX 출력, PDF가 HWPX-first인지 | SQLite + 가짜 템플릿 |
| `test_hwpx_pdf_renderer.py` | PDF 생성 파이프라인, 미리보기 API, 날짜 역전 검증 | FakeRenderer 주입 |
| `test_hwpx_repeat_pages.py` | **실제 반복 페이지 템플릿**으로 페이지 수 검증 (식단표 2/3/4/5/6주, 조리지시서 1/2/5/10일, 보존식 1/3/4/6/7/10식) | `docs/template/*.hwpx` 실제 파일 |
| `test_master_data.py` | HWPX 검증(필수 파일/ZIP Slip/플레이스홀더/spine), 템플릿 CRUD/활성화/삭제 제한 | 가짜 템플릿 |

**자동 테스트 한계** (수동 확인 필요):
- 한글에서 정상 열림/복구 메시지 없음
- 글자/표 깨짐 없음
- 편집·저장·재열기 가능 여부
- PDF 레이아웃 일치

## 7. Windows 재개발 시 문서 시스템 방향

### 재사용 가능한 자산
1. **HWPX 반복 페이지 템플릿 3종** (`docs/template/*.hwpx`) — 파일 자체는 그대로 재사용 가능
2. **플레이스홀더 규칙** (`REQUIRED_PLACEHOLDERS`, `TEMPLATE_PAGE_MODEL.md`) — 동일 규칙 유지
3. **페이지 수 규칙** (`template-page-config.json`) — 동일 규칙 유지
4. **한컴오피스 COM PDF 변환** — Windows 네이티브에서는 오히려 더 안정적으로 사용 가능 (별도 서브프로세스 불필요 가능)
5. **문서 DTO 구조** — C# record/class로 동일하게 설계 가능

### 재작성 필요 자산
1. `hwpx_engine.py` (1,250줄) — C#으로 재작성 필요. XML 조작은 `System.Xml.Linq`로 가능
2. `document_builders.py` / `document_dtos.py` — C#으로 재작성
3. HTML 미리보기 템플릿 — WPF에서는 미리보기 방식 재설계 (WebView2 또는 WPF 렌더링)
4. `hwpx_service.py`의 구버전/신버전 이중 구현 — C#에서는 단일 구현으로 정리

### Windows COM 연동 가능 영역
- `HWPFrame.HwpObject` 직접 Dispatch (Python은 서브프로세스로 격리했지만, WPF는 동일 프로세스 COM 가능)
- `HwpObject.Open/SaveAs` — HWPX 열기/PDF 저장
- `RegisterModule("FilePathCheckDLL", "FilePathCheckerModule")` — 보안 모듈 등록
- 한컴오피스 설치 여부/버전 감지, PDF 프린터 의존성 확인 필요
- 대안: 한컴오피스가 없는 환경을 위해 `hwpx_engine`만으로 HWPX 생성은 가능하나 PDF 변환은 불가 (기존과 동일 제약)

## 8. 미확인 사항

- `docs/template/TEMPLATE_FIELDS.md` — 참조만 존재, 파일 없음 (플레이스홀더 전체 문서)
- `hwpx-output-analysis.md` — 미리보기 레이아웃 분석 문서 (내용 미검토)
- 반복 페이지 템플릿의 실제 표 구조/셀 병합 세부 — HWPX XML 내부 확인 필요
- `applyRepeatPages` 실패 시 폴백 경로의 출력 품질 — 테스트는 반복 페이지 경로만 검증
