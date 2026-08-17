using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KpicCafeteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    entity_id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    detail = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "backup_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    stored_filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    file_size = table.Column<int>(type: "INTEGER", nullable: true),
                    backup_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    checksum_sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "data_archives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    stored_filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    file_size = table.Column<int>(type: "INTEGER", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    date_from = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    date_to = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    expires_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_archives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    document_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    original_filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    stored_filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    storage_path = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    file_size = table.Column<int>(type: "INTEGER", nullable: true),
                    checksum_sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    active = table.Column<bool>(type: "INTEGER", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    is_valid = table.Column<bool>(type: "INTEGER", nullable: false),
                    validation_message = table.Column<string>(type: "TEXT", nullable: true),
                    placeholder_summary = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true),
                    created_by = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    token = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    storage_path = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false),
                    errors = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    completed_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    stat_group = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    default_unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    kg_factor = table.Column<double>(type: "REAL", nullable: true),
                    analysis_excluded = table.Column<bool>(type: "INTEGER", nullable: false),
                    active = table.Column<bool>(type: "INTEGER", nullable: false),
                    review_status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "meal_services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    service_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    meal_type = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    planned_count = table.Column<int>(type: "INTEGER", nullable: false),
                    service_time = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    concept_title = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    meal_plan_output_at = table.Column<string>(type: "TEXT", nullable: true),
                    cooking_output_at = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "meal_type_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    default_planned_count = table.Column<int>(type: "INTEGER", nullable: false),
                    default_service_time = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    active = table.Column<bool>(type: "INTEGER", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_type_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    canonical_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    active = table.Column<bool>(type: "INTEGER", nullable: false),
                    review_status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingredient_aliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    alias = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ingredient_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredient_aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ingredient_aliases_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ingredient_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ingredient_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    order_quantity = table.Column<double>(type: "REAL", nullable: true),
                    order_unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    order_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    delivery_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    total_required_quantity = table.Column<double>(type: "REAL", nullable: true),
                    required_unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_groups_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meal_actuals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    meal_service_id = table.Column<int>(type: "INTEGER", nullable: false),
                    actual_count = table.Column<int>(type: "INTEGER", nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_actuals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meal_actuals_meal_services_meal_service_id",
                        column: x => x.meal_service_id,
                        principalTable: "meal_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preservation_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    meal_service_id = table.Column<int>(type: "INTEGER", nullable: false),
                    collected_at = table.Column<string>(type: "TEXT", nullable: true),
                    manager_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    freezer_temperature = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    disposal_at = table.Column<string>(type: "TEXT", nullable: true),
                    collector_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    collection_time = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    completed_at = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preservation_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_preservation_records_meal_services_meal_service_id",
                        column: x => x.meal_service_id,
                        principalTable: "meal_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    menu_id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    composition_key = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    is_default = table.Column<bool>(type: "INTEGER", nullable: false),
                    active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipes_menus_menu_id",
                        column: x => x.menu_id,
                        principalTable: "menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    service_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ingredient_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ingredient_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    required_quantity = table.Column<double>(type: "REAL", nullable: true),
                    required_unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    order_quantity = table.Column<double>(type: "REAL", nullable: true),
                    order_unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    order_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    delivery_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    order_group_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_order_items_order_groups_order_group_id",
                        column: x => x.order_group_id,
                        principalTable: "order_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meal_service_menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    meal_service_id = table.Column<int>(type: "INTEGER", nullable: false),
                    menu_id = table.Column<int>(type: "INTEGER", nullable: true),
                    recipe_id = table.Column<int>(type: "INTEGER", nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    menu_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    recipe_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    recipe_version_snapshot = table.Column<int>(type: "INTEGER", nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    is_representative = table.Column<bool>(type: "INTEGER", nullable: false),
                    cooking_instruction = table.Column<string>(type: "TEXT", nullable: true),
                    cooking_note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_service_menus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meal_service_menus_meal_services_meal_service_id",
                        column: x => x.meal_service_id,
                        principalTable: "meal_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meal_service_menus_menus_menu_id",
                        column: x => x.menu_id,
                        principalTable: "menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_meal_service_menus_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    recipe_id = table.Column<int>(type: "INTEGER", nullable: false),
                    ingredient_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    quantity_per_100 = table.Column<double>(type: "REAL", nullable: true),
                    unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    is_primary = table.Column<bool>(type: "INTEGER", nullable: false),
                    review_status = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_service_menu_ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    meal_service_menu_id = table.Column<int>(type: "INTEGER", nullable: false),
                    ingredient_id = table.Column<int>(type: "INTEGER", nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    ingredient_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    quantity_total = table.Column<double>(type: "REAL", nullable: true),
                    quantity_per_100 = table.Column<double>(type: "REAL", nullable: true),
                    unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    source_note = table.Column<string>(type: "TEXT", nullable: true),
                    source_row = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_service_menu_ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meal_service_menu_ingredients_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_meal_service_menu_ingredients_meal_service_menus_meal_service_menu_id",
                        column: x => x.meal_service_menu_id,
                        principalTable: "meal_service_menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_action",
                table: "audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_document_templates_active",
                table: "document_templates",
                column: "active");

            migrationBuilder.CreateIndex(
                name: "IX_document_templates_document_type",
                table: "document_templates",
                column: "document_type");

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_token",
                table: "import_jobs",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_aliases_alias",
                table: "ingredient_aliases",
                column: "alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingredient_aliases_ingredient_id",
                table: "ingredient_aliases",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_ingredients_name",
                table: "ingredients",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingredients_source_code",
                table: "ingredients",
                column: "source_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingredients_stat_group",
                table: "ingredients",
                column: "stat_group");

            migrationBuilder.CreateIndex(
                name: "IX_meal_actuals_meal_service_id",
                table: "meal_actuals",
                column: "meal_service_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_service_menu_ingredients_ingredient_id",
                table: "meal_service_menu_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_service_menu_ingredients_meal_service_menu_id",
                table: "meal_service_menu_ingredients",
                column: "meal_service_menu_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_service_menus_meal_service_id",
                table: "meal_service_menus",
                column: "meal_service_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_service_menus_menu_id",
                table: "meal_service_menus",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_service_menus_recipe_id",
                table: "meal_service_menus",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_services_meal_type",
                table: "meal_services",
                column: "meal_type");

            migrationBuilder.CreateIndex(
                name: "IX_meal_services_service_date",
                table: "meal_services",
                column: "service_date");

            migrationBuilder.CreateIndex(
                name: "uq_meal_service_date_type",
                table: "meal_services",
                columns: new[] { "service_date", "meal_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_type_settings_code",
                table: "meal_type_settings",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_type_settings_name",
                table: "meal_type_settings",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_menus_canonical_name",
                table: "menus",
                column: "canonical_name");

            migrationBuilder.CreateIndex(
                name: "IX_menus_name",
                table: "menus",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_menus_role",
                table: "menus",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "IX_menus_source_code",
                table: "menus",
                column: "source_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_groups_ingredient_id",
                table: "order_groups",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ingredient_id",
                table: "order_items",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_group_id",
                table: "order_items",
                column: "order_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_service_date",
                table: "order_items",
                column: "service_date");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_status",
                table: "order_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_order_item_date_ingredient",
                table: "order_items",
                columns: new[] { "service_date", "ingredient_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_preservation_records_meal_service_id",
                table: "preservation_records",
                column: "meal_service_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_ingredient_id",
                table: "recipe_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_recipe_id",
                table: "recipe_ingredients",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_active",
                table: "recipes",
                column: "active");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_composition_key",
                table: "recipes",
                column: "composition_key");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_is_default",
                table: "recipes",
                column: "is_default");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_menu_id",
                table: "recipes",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "uq_recipe_menu_composition",
                table: "recipes",
                columns: new[] { "menu_id", "composition_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_recipe_menu_version",
                table: "recipes",
                columns: new[] { "menu_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "backup_records");

            migrationBuilder.DropTable(
                name: "data_archives");

            migrationBuilder.DropTable(
                name: "document_templates");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "ingredient_aliases");

            migrationBuilder.DropTable(
                name: "meal_actuals");

            migrationBuilder.DropTable(
                name: "meal_service_menu_ingredients");

            migrationBuilder.DropTable(
                name: "meal_type_settings");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "preservation_records");

            migrationBuilder.DropTable(
                name: "recipe_ingredients");

            migrationBuilder.DropTable(
                name: "meal_service_menus");

            migrationBuilder.DropTable(
                name: "order_groups");

            migrationBuilder.DropTable(
                name: "meal_services");

            migrationBuilder.DropTable(
                name: "recipes");

            migrationBuilder.DropTable(
                name: "ingredients");

            migrationBuilder.DropTable(
                name: "menus");
        }
    }
}
