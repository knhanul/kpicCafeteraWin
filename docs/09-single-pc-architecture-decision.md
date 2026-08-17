# 09. Single-PC 아키텍처 결정

> 2단계(Core Foundation)에서 확정된 Windows 버전 아키텍처 결정 사항.
> 작성일: 2026-08-15

## 1. 최종 결정 사항

Windows 버전은 **1대의 Windows PC에서 1명의 사용자가 사용하는 로컬 프로그램**만을 대상으로 한다.

| 항목 | 결정 |
| --- | --- |
| 사용자 환경 | 1PC · 1인 사용자 |
| DB | SQLite (EF Core 10 + Microsoft.EntityFrameworkCore.Sqlite) |
| 로그인 | 없음 (로그인 화면/사용자 ID/비밀번호 미구현) |
| users 테이블 | 생성하지 않음 |
| REST API | 없음 (WPF ViewModel → Application Service 직접 호출) |
| PostgreSQL | 사용하지 않음 |
| Docker/Nginx | 사용하지 않음 |
| Document Preview Token | 사용하지 않음 (`document_previews` 테이블 미생성) |
| 세션/쿠키 | 사용하지 않음 |
| admin/user 권한 | 없음 |
| must_change_password | 없음 |
| 최소 admin 1명 규칙 | 없음 |

## 2. 데이터 흐름

```text
WPF Desktop
      ↓
Application (Service/Interface)
      ↓
Domain (Entity/규칙)
      ↓
EF Core
      ↓
SQLite (%LOCALAPPDATA%\KpicCafeteria\Data\cafeteria.db)
```

## 3. DB 저장 위치

- 기본 위치: `%LOCALAPPDATA%\KpicCafeteria`
- DB 파일: `%LOCALAPPDATA%\KpicCafeteria\Data\cafeteria.db`
- 실행파일 위치 / Program Files 내부에는 저장하지 않는다.
- `IAppDataPathProvider` / `AppDataPathProvider`가 모든 경로를 제공하며, 접근 시 디렉터리를 자동 생성한다.

| 경로 | 값 |
| --- | --- |
| DataDirectory | `%LOCALAPPDATA%\KpicCafeteria\Data` |
| DatabasePath | `%LOCALAPPDATA%\KpicCafeteria\Data\cafeteria.db` |
| TemplateDirectory | `%LOCALAPPDATA%\KpicCafeteria\Templates` |
| BackupDirectory | `%LOCALAPPDATA%\KpicCafeteria\Backups` |
| ArchiveDirectory | `%LOCALAPPDATA%\KpicCafeteria\Archives` |
| TempDirectory | `%LOCALAPPDATA%\KpicCafeteria\Temp` |
| LogDirectory | `%LOCALAPPDATA%\KpicCafeteria\Logs` |

## 4. SQLite 운영 설정

- **Foreign Keys**: 연결 문자열 `Foreign Keys=True`로 활성화 (EF Core 기본값과 명시적 지정 병행)
- **WAL mode**: `PRAGMA journal_mode=WAL` — 단일 프로세스 데스크톱 앱에 적합 (읽기/쓰기 동시성 향상, DB 파일 + `-wal`/`-shm` 파일 생성)
- **Busy timeout**: `PRAGMA busy_timeout=5000` (5초)
- **Command timeout**: 30초
- Connection pooling / 다중 사용자 locking 시스템은 만들지 않는다 (단일 프로세스 전제)

## 5. 제거된 기존 기능 (R24 제외)

기존 업무규칙 중 **사용자 관련 규칙 R24 전체**는 Windows 버전 적용 대상에서 제외한다.

- R24-1 비밀번호 최소 8자 / ID 동일 금지
- R24-2 must_change_password
- R24-3 본인 계정 비활성화 금지
- R24-4 최소 1명의 활성 admin
- R24-5 admin 전용 화면 (사용자 관리/백업/아카이브)

기존 DB 이관 시 사용자 데이터는 업무 핵심 데이터와 무관하므로 이관하지 않는다.

## 6. 기존 분석 문서 중 더 이상 적용되지 않는 부분

| 문서 | 적용 제외 내용 |
| --- | --- |
| `01-current-system-analysis.md` | FastAPI/REST API/세션 쿠키/Nginx/Docker/PostgreSQL 서버 구조 전체 |
| `02-feature-inventory.md` | 1-1 로그인/로그아웃, 1-2 비밀번호 변경, 1-3 사용자 관리, 6-1 미리보기 토큰 |
| `03-domain-model-analysis.md` | users, document_previews 테이블 (신규 DB에서 미생성) |
| `04-business-rules.md` | R24 (사용자/권한) 전체 |
| `06-windows-architecture-proposal.md` | PostgreSQL Provider, 세션/쿠키 인증, 미리보기 토큰 관련 설계 |
| `07-migration-mapping.md` | 사용자 관리/인증/미리보기 토큰 항목 (제거 대상) |

## 7. 유지되는 핵심 규칙

- 스냅샷 구조 (MenuNameSnapshot/RecipeId/RecipeNameSnapshot/RecipeVersionSnapshot, IngredientId/IngredientNameSnapshot/QuantityTotal/QuantityPer100/Unit)
- CompositionKey 규칙 (재료 ID 정렬 + `,` 결합, 빈 값 `"EMPTY"`, 수량·단위 제외)
- 수량 환산 규칙 (per_100 × planned / 100, planned=0이면 역산 안 함)
- 삭제 정책 (기준정보 미사용 처리, 식단 cascade, 스냅샷 FK SET NULL)
- Unique 제약 (menus.name, ingredients.name, meal_services(service_date, meal_type), recipes(menu_id, version), recipes(menu_id, composition_key), ingredient_aliases.alias, order_items(service_date, ingredient_id))
- DB 테이블명/컬럼명 snake_case 유지 (기존 데이터 이관/비교 용이)
- 코드 값 문자열 호환 (LUNCH/DINNER, pending/ordered/skipped)
