"""신규 Windows SQLite DB 검증 스크립트 (2단계 실행 검증용)."""
import os
import sqlite3

db_path = os.path.join(os.environ["LOCALAPPDATA"], "KpicCafeteria", "Data", "cafeteria.db")
print("DB path:", db_path)
print("DB exists:", os.path.exists(db_path))

con = sqlite3.connect(db_path)

print("\n-- meal_type_settings --")
for row in con.execute(
    "select code, name, default_planned_count, default_service_time, sort_order, active "
    "from meal_type_settings order by sort_order"
):
    print(row)

print("\n-- tables --")
tables = [
    r[0]
    for r in con.execute(
        "select name from sqlite_master where type='table' "
        "and name not like 'sqlite_%' and name != '__EFMigrationsHistory' order by name"
    )
]
print(tables)
print("table count:", len(tables))

print("\n-- excluded tables --")
print("users exists:", "users" in tables)
print("document_previews exists:", "document_previews" in tables)

con.close()
