# 03. DB 모델 및 업무 Entity 관계 분석

> 출처: `backend/app/models.py` (SQLAlchemy 2, 20개 테이블)

## 1. 전체 ERD

```text
users ──────────────┐
                    │
meal_type_settings  │  (배식유형: LUNCH/DINNER)
                    │
menus ──1:N── recipes ──1:N── recipe_ingredients ──N:1── ingredients ──1:N── ingredient_aliases
  │                   │                                      │
  │                   └──(recipe_id, SET NULL)               │
  │                                                          │
  └──1:N── meal_service_menus ──1:N── meal_service_menu_ingredients ──N:1──┘
              │                          │
              │                          └──(ingredient_id, SET NULL)
              │
meal_services ──1:N── meal_service_menus
  │
  ├──1:1── preservation_records
  └──1:1── meal_actuals

order_groups ──1:N── order_items ──N:1── ingredients (SET NULL)

document_templates / document_previews / import_jobs / audit_logs / backup_records / data_archives
```

## 2. 테이블 상세

### 2-1. users — 사용자
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| id | PK | |
| username | String(80) UNIQUE | 로그인 ID |
| password_hash | String(300) | `pbkdf2_sha256$240000$salt$digest` |
| display_name | String(100) | 기본 "영양사" |
| active | Boolean | 비활성 시 로그인 차단 |
| role | String(20) | `admin` / `user` |
| must_change_password | Boolean | 최초 로그인/초기화 후 변경 강제 |
| password_changed_at / last_login_at | DateTime(tz) | |
| created_at / updated_at | DateTime(tz) | |

### 2-2. meal_type_settings — 배식유형 설정
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| code | String(30) UNIQUE | `LUNCH` / `DINNER` |
| name | String(30) UNIQUE | 중식 / 석식 |
| default_planned_count | Integer | 기본 계획식수 (중식 400, 석식 100) |
| default_service_time | Time | 기본 배식시간 (11:40 / 17:30) |
| sort_order | Integer | 화면 정렬 |
| active | Boolean | 사용여부 |
| description | Text | |

### 2-3. menus — 메뉴 기준정보
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| source_code | String(30) UNIQUE | 이관 원본 코드 |
| name | String(200) UNIQUE | 메뉴명 (중복 금지) |
| canonical_name | String(200) | 통계집계메뉴명 |
| role | String(40) | 메뉴역할: 밥·죽/면·떡/국·탕/찌개·전골/주찬/부찬/김치·절임/샐러드/후식·음료/기타 |
| active | Boolean | 미사용 처리 (삭제 대체) |
| review_status | String(40) | 정상 / 검토 필요 등 |

관계: `recipes` 1:N (cascade delete-orphan, version 정렬)

### 2-4. ingredients — 재료 기준정보
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| source_code | String(30) UNIQUE | 이관 원본 코드 |
| name | String(200) UNIQUE | 표준재료명 |
| stat_group | String(60) | 통계분석군: 곡류·주식/면·떡·전분/소고기/돼지고기/닭·오리/수산물/달걀/두류·두부/채소/버섯·해조/과일·견과/유제품/김치·절임/가공식품/장류·소스·조미료/기타 |
| default_unit | String(30) | kg/g/L/ml/개/봉/팩/판/통/캔/병/박스/단/묶음/장/줄/포/관/밧트 |
| kg_factor | Float | kg 환산계수 (통계 중량 계산용) |
| analysis_excluded | Boolean | 통계 분석 제외 |
| active | Boolean | 미사용 처리 |
| review_status | String(40) | `자동등록-분류필요` 등 |

관계: `ingredient_aliases` 1:N (cascade delete-orphan)

### 2-5. ingredient_aliases — 재료 별칭
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| alias | String(200) UNIQUE | 원재료별칭 (검색 키) |
| ingredient_id | FK → ingredients (CASCADE) | |
| source | String(80) | 기존데이터 / 사용자 |

### 2-6. recipes — 레시피 (다중 레시피)
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| menu_id | FK → menus (CASCADE) | |
| name | String(120) | 기본 "기본 레시피" |
| version | Integer | 메뉴별 순차 버전 |
| composition_key | String(1000) | 재료 ID 정렬 집합 `"1,2,5"` / `"EMPTY"` |
| note | Text | |
| is_default | Boolean | 메뉴당 1개 (식단 추가 시 기본 선택) |
| active | Boolean | 미사용 처리 |

제약:
- `uq_recipe_menu_version` (menu_id, version)
- `uq_recipe_menu_composition` (menu_id, composition_key)

관계: `recipe_ingredients` 1:N (cascade delete-orphan, sort_order 정렬)

### 2-7. recipe_ingredients — 레시피 재료 (100인 기준)
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| recipe_id | FK → recipes (CASCADE) | |
| ingredient_id | FK → ingredients | |
| sort_order | Integer | |
| quantity_per_100 | Float | **100인 기준 수량** |
| unit | String(30) | 재료 기본단위 fallback |
| is_primary | Boolean | 주재료 표시 |
| review_status | String(60) | |

### 2-8. meal_services — 배식 (식단 일자)
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| service_date | Date | |
| meal_type | String(30) | LUNCH / DINNER |
| planned_count | Integer | 계획식수 |
| service_time | Time | 배식시간 |
| concept_title | String(80) | 콘셉트 (예: "여름 보양식") |
| note | Text | |
| meal_plan_output_at | DateTime(tz) | 식단표 출력 시각 |
| cooking_output_at | DateTime(tz) | 조리지시서 출력 시각 |

제약: `uq_meal_service_date_type` (service_date, meal_type)

관계:
- `meal_service_menus` 1:N (cascade delete-orphan, sort_order 정렬)
- `preservation_records` 1:1 (cascade delete-orphan)
- `meal_actuals` 1:1 (cascade delete-orphan)

### 2-9. meal_service_menus — 식단 메뉴 (스냅샷)
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| meal_service_id | FK → meal_services (CASCADE) | |
| menu_id | FK → menus (SET NULL) | 기준 메뉴 참조 (삭제돼도 스냅샷 유지) |
| recipe_id | FK → recipes (SET NULL) | 적용 레시피 참조 |
| sort_order | Integer | |
| menu_name_snapshot | String(200) | **메뉴명 스냅샷** |
| recipe_name_snapshot | String(120) | **레시피명 스냅샷** |
| recipe_version_snapshot | Integer | **레시피 버전 스냅샷** |
| note | Text | 메뉴 비고 |
| is_representative | Boolean | 대표 메뉴 (배식당 1개, 주찬 자동 지정) |
| cooking_instruction | Text | 조리지시 |
| cooking_note | Text | 조리비고 |

관계: `meal_service_menu_ingredients` 1:N (cascade delete-orphan, sort_order 정렬)

### 2-10. meal_service_menu_ingredients — 식단 재료 스냅샷
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| meal_service_menu_id | FK → meal_service_menus (CASCADE) | |
| ingredient_id | FK → ingredients (SET NULL) | |
| sort_order | Integer | |
| ingredient_name_snapshot | String(200) | **재료명 스냅샷** |
| quantity_total | Float | **총 수량** (계획식수 기준) |
| quantity_per_100 | Float | 100인 기준 수량 (역산 가능) |
| unit | String(30) | |
| source_note | Text | 원본 비고 |
| source_row | Text | 이관 원본 행 |

### 2-11. preservation_records — 보존식 기록 (1:1)
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| meal_service_id | FK UNIQUE → meal_services (CASCADE) | |
| collected_at | DateTime(tz) | 채수일시 |
| manager_name | String(100) | 관리자 |
| freezer_temperature | String(30) | 냉동고 온도 (문자열) |
| disposal_at | DateTime(tz) | 폐기일시 |
| collector_name | String(100) | 채수자 |
| collection_time | String(20) | 채수시간 (HH:MM 문자열) |
| note | Text | |
| completed_at | DateTime(tz) | 완료 시각 (completed 체크 시 기록) |
| updated_at | DateTime(tz) | |

### 2-12. meal_actuals — 실제 식수 (1:1)
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| meal_service_id | FK UNIQUE → meal_services (CASCADE) | |
| actual_count | Integer | 실제 식수 |
| note | Text | |
| recorded_at | DateTime(tz) | 입력 시각 |

### 2-13. document_templates — HWPX 템플릿
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| document_type | String(50) | MEAL_PLAN / COOKING_INSTRUCTION / PRESERVATION_RECORD |
| name | String(160) | |
| description | Text | |
| original_filename / stored_filename | String(255) | |
| storage_path | String(600) | 파일 경로 |
| file_size | Integer | |
| checksum_sha256 | String(64) | |
| active | Boolean | 유형당 1개 활성 |
| version | Integer | 유형별 순차 버전 |
| is_valid / validation_message | Boolean/Text | 검증 상태 |
| placeholder_summary | JSON | 검출된 플레이스홀더 목록 |
| created_by | String(80) | |

### 2-14. document_previews — 미리보기 토큰
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| token | String(80) PK | |
| document_type | String(50) | |
| payload | JSON | 렌더링 payload 스냅샷 |
| service_ids | JSON | 대상 배식 ID 목록 |
| user_id | FK → users | 소유자 |
| expires_at | DateTime(tz) | 6시간 |

### 2-15. import_jobs — 이관 작업
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| token | String(80) UNIQUE | |
| filename / storage_path | | |
| status | String(30) | PREVIEWED / INVALID / COMPLETED / FAILED |
| summary / errors | JSON | |
| completed_at | DateTime(tz) | |

### 2-16. audit_logs — 감사 로그
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| user_id | FK → users | |
| action | String(80) | user.create / backup_create / MIGRATION_IMPORT 등 |
| entity_type / entity_id | String(80) | |
| detail | JSON | |
| created_at | DateTime(tz) | |

### 2-17. backup_records — 백업 기록
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| filename / stored_filename | String(255) | |
| file_size | Integer | |
| backup_type | String(20) | manual / auto |
| status | String(30) | |
| checksum_sha256 | String(64) | |
| created_by | String(80) | |

### 2-18. data_archives — Excel 아카이브 기록
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| filename / stored_filename | String(255) | |
| file_size | Integer | |
| status | String(30) | completed / expired |
| date_from / date_to | Date | |
| expires_at | DateTime(tz) | 24시간 |

### 2-19. order_groups — 발주 그룹
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| ingredient_id | FK → ingredients (SET NULL) | |
| ingredient_name_snapshot | String(200) | |
| order_quantity / order_unit | Float/String | 묶음 발주량 |
| order_date / delivery_date | Date | |
| total_required_quantity | Float | 항목 필요량 합계 |
| required_unit | String(30) | |
| created_by | String(80) | |

### 2-20. order_items — 발주 항목
| 컬럼 | 타입 | 설명 |
| --- | --- | --- |
| service_date | Date | 사용일 |
| ingredient_id | FK → ingredients (SET NULL) | |
| ingredient_name_snapshot | String(200) | |
| required_quantity / required_unit | Float/String | 식단 집계 필요량 |
| order_quantity / order_unit | Float/String | 사용자 발주량 |
| order_date / delivery_date | Date | |
| status | String(20) | pending / ordered / skipped |
| order_group_id | FK → order_groups (SET NULL) | |

제약: `uq_order_item_date_ingredient` (service_date, ingredient_id) — 단, ingredient_id NULL인 행은 (service_date, 이름) 기준

## 3. 핵심 설계 포인트

### 3-1. 스냅샷 패턴
식단(MealServiceMenu)에 메뉴를 추가할 때:
1. `menu_name_snapshot` = 메뉴명 복사
2. `recipe_id`/`recipe_name_snapshot`/`recipe_version_snapshot` = 선택 레시피 참조 + 복사
3. 레시피 재료 전체를 `MealServiceMenuIngredient`로 복사 (`ingredient_name_snapshot`, `quantity_total`, `quantity_per_100`, `unit`)

이후 기준 레시피가 수정/삭제되어도 과거 식단의 재료는 자동 변경되지 않는다. `menu_id`/`ingredient_id` FK는 SET NULL이므로 기준정보 삭제(미사용)에도 스냅샷은 보존된다.

### 3-2. composition_key (레시피 구분)
- 재료 ID 집합을 정렬해 `","`로 결합한 문자열
- **수량과 단위는 포함하지 않음** → 재료가 같고 수량만 다르면 기존 레시피 수정
- `EMPTY`는 재료 없는 레시피
- 이관 시 `_group_recipe_rows_by_composition`이 sort_order 역전(블록 경계)을 기준으로 레시피 블록을 분리

### 3-3. quantity_total / quantity_per_100 관계
- 기준 레시피: `quantity_per_100` (100인 기준)
- 식단 스냅샷: `quantity_total` (계획식수 기준) + `quantity_per_100` (역산 보존)
- 환산 공식: `quantity_total = quantity_per_100 × planned_count / 100`
- 계획식수 변경 시 `update_service`가 스냅샷의 `quantity_total`을 재계산
- 재료 편집 시 한쪽만 입력하면 반대쪽 역산 (`per_100 = total × 100 / planned`)

### 3-4. 대표 메뉴 (is_representative)
- 배식당 최대 1개
- 메뉴 추가 시 첫 `주찬` 메뉴에 자동 지정
- 일괄 저장 시 첫 True만 승인, 해제 가능

### 3-5. 발주 키 규칙
- 집계 키: `(service_date, ingredient_id)` — ingredient_id 없으면 `(service_date, "name:{이름}")`
- OrderItem unique: `(service_date, ingredient_id)` — NULL 재료는 이름 기준
- 식단에서 사라진 항목도 `in_plan=false`로 유지 (사용자 입력 보존)

### 3-6. 삭제 정책
- 메뉴/재료/레시피: **미사용 처리** (`active=false`) — 물리 삭제 아님
- 배식/식단 메뉴: 물리 삭제 (cascade)
- FK: `SET NULL` + 스냅샷 컬럼으로 과거 데이터 보존

## 4. Windows 재설계 시 유의점

1. **스냅샷 컬럼 3종**(menu/recipe/ingredient name + recipe version)은 반드시 유지 — 과거 데이터 무결성의 핵심
2. **composition_key** 로직(수량·단위 제외, ID 정렬)은 레시피 중복 판정의 기준 — 동일 규칙 유지 필요
3. **quantity_per_100 ↔ quantity_total** 환산 규칙은 문서/발주/통계 전반에 영향 — 단일 규칙으로 캡슐화 필요
4. `meal_type`은 문자열 코드(LUNCH/DINNER)로 `meal_type_settings`와 연결 — enum으로 강화 가능하나 기존 데이터 호환 필요
5. `freezer_temperature`/`collection_time`은 문자열 — 입력 형식 자유 (검증 없음)
6. JSON 컬럼(payload, summary, errors, detail, placeholder_summary)은 SQLite/PostgreSQL 모두 사용 — .NET에서는 JSON 컬럼 매핑 필요
7. `document_previews.payload`는 렌더링 스냅샷 — Windows에서는 파일 기반 미리보기로 대체 가능
