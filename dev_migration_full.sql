/* =====================================================================
   Dong Nai GridSmart - DATABASE KIEM THU (schema + toan bo du lieu test)
   Sinh tu dong boi database/tao_dev_migration.ps1 luc 2026-10-13T08:59:13 UTC. KHONG sua tay.

   Chi dung cho may phat trien / kiem thu. KHONG dung cho Production
   (moi tai khoan cung mot mat khau cong khai). Production: prd_migration_full.sql.

   Noi dung: database dongnai_gridsmart, 18 bang, trigger, 151 view/function/proc,
   713 dong du lieu (19 tai khoan, 40 phan anh, 54 o luoi...).
   Mat khau MOI tai khoan: GridSmart@2026
   Moc thoi gian duoc doi theo luc chay file, giu nguyen "x ngay truoc".

   Cach chay (chon mot):
     sqlcmd -S "lpc:." -E -b -I -f 65001 -i dev_migration_full.sql
     SSMS: mo file, Execute (F5). Khong can SQLCMD Mode.

   - Chay tren may CHUA co database dongnai_gridsmart (hoac co nhung rong).
     Neu database da co du lieu, file dung ngay va khong dong vao du lieu.
     Muon nap lai tu dau: DROP DATABASE dongnai_gridsmart; roi chay lai.
   - Chay trong mot giao dich: loi o dau thi rollback toan bo.
   - Anh phan anh (duong dan uploads/seed/<loai>_before.jpg ...) nam trong source backend:
     backend/GridSmart.Api/wwwroot/uploads/seed. Thieu thi chay tao_anh_mau.ps1.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
GO

IF DB_ID(N'dongnai_gridsmart') IS NULL
    CREATE DATABASE dongnai_gridsmart COLLATE Vietnamese_100_CI_AS;
GO
USE dongnai_gridsmart;
GO

/* Khong ghi de database dang co du lieu */
IF OBJECT_ID(N'dbo.tbl_users', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.tbl_users)
    BEGIN
        RAISERROR (N'Database dongnai_gridsmart đã có dữ liệu, dừng lại để không ghi đè. Muốn nạp lại: DROP DATABASE dongnai_gridsmart; rồi chạy lại file.', 16, 1);
        SET NOEXEC ON;
    END;
END;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO
/* =====================================================================
   2. Bang, index, constraint
   ===================================================================== */

/* ---------- tbl_admin_levels ---------- */
IF OBJECT_ID(N'dbo.tbl_admin_levels', N'U') IS NULL
CREATE TABLE tbl_admin_levels (
    id           TINYINT       NOT NULL,
    level_code   VARCHAR(30)   NOT NULL,            -- PROVINCE, COMMUNE, HAMLET
    level_name   NVARCHAR(100) NOT NULL,
    level_order  TINYINT       NOT NULL,            -- 1 la cap cao nhat, so cang lon cang thap
    CONSTRAINT pk_tbl_admin_levels PRIMARY KEY (id),
    CONSTRAINT uq_tbl_admin_levels_level_code UNIQUE (level_code),
    CONSTRAINT uq_tbl_admin_levels_level_order UNIQUE (level_order)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_admin_unit_types ---------- */
IF OBJECT_ID(N'dbo.tbl_admin_unit_types', N'U') IS NULL
CREATE TABLE tbl_admin_unit_types (
    id          TINYINT       NOT NULL,
    type_code   VARCHAR(30)   NOT NULL,             -- TINH, THANH_PHO, PHUONG, XA, DAC_KHU, KHU_PHO, THON, AP, TO_DAN_PHO
    type_name   NVARCHAR(100) NOT NULL,
    level_id    TINYINT       NOT NULL,
    CONSTRAINT pk_tbl_admin_unit_types PRIMARY KEY (id),
    CONSTRAINT uq_tbl_admin_unit_types_type_code UNIQUE (type_code),
    CONSTRAINT fk_tbl_admin_unit_types_level_id FOREIGN KEY (level_id) REFERENCES tbl_admin_levels (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_admin_units (cay don vi hanh chinh) ---------- */
IF OBJECT_ID(N'dbo.tbl_admin_units', N'U') IS NULL
CREATE TABLE tbl_admin_units (
    id                  INT           IDENTITY(1,1) NOT NULL,
    unit_code           VARCHAR(30)   NOT NULL,     -- ma DVHC chinh thuc neu co
    name                NVARCHAR(150) NOT NULL,
    unit_type_id        TINYINT       NOT NULL,
    parent_id           INT           NULL,         -- NULL voi cap cao nhat
    unit_path           VARCHAR(400)  NULL,         -- trigger tu tinh, VD '/1/4/12/'
    boundary            GEOGRAPHY     NULL,
    is_active           BIT           NOT NULL CONSTRAINT df_tbl_admin_units_is_active DEFAULT (1),
    effective_from      DATE          NULL,
    effective_to        DATE          NULL,
    merged_into_unit_id INT           NULL,
    created_at          DATETIME2(0)  NOT NULL CONSTRAINT df_tbl_admin_units_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at          DATETIME2(0)  NULL,
    CONSTRAINT pk_tbl_admin_units PRIMARY KEY (id),
    CONSTRAINT uq_tbl_admin_units_unit_code UNIQUE (unit_code),
    CONSTRAINT fk_tbl_admin_units_unit_type_id FOREIGN KEY (unit_type_id) REFERENCES tbl_admin_unit_types (id),
    CONSTRAINT fk_tbl_admin_units_parent_id FOREIGN KEY (parent_id) REFERENCES tbl_admin_units (id),
    CONSTRAINT fk_tbl_admin_units_merged_into FOREIGN KEY (merged_into_unit_id) REFERENCES tbl_admin_units (id),
    CONSTRAINT ck_tbl_admin_units_not_own_parent CHECK (parent_id IS NULL OR parent_id <> id),
    CONSTRAINT ck_tbl_admin_units_effective CHECK (effective_to IS NULL OR effective_from IS NULL OR effective_to >= effective_from)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_admin_units_parent_id' AND object_id = OBJECT_ID(N'dbo.tbl_admin_units'))
    CREATE INDEX ix_tbl_admin_units_parent_id ON tbl_admin_units (parent_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_admin_units_unit_path' AND object_id = OBJECT_ID(N'dbo.tbl_admin_units'))
    CREATE INDEX ix_tbl_admin_units_unit_path ON tbl_admin_units (unit_path);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'six_tbl_admin_units_boundary' AND object_id = OBJECT_ID(N'dbo.tbl_admin_units'))
    CREATE SPATIAL INDEX six_tbl_admin_units_boundary ON tbl_admin_units (boundary) USING GEOGRAPHY_AUTO_GRID;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_roles ---------- */
IF OBJECT_ID(N'dbo.tbl_roles', N'U') IS NULL
CREATE TABLE tbl_roles (
    id          INT           IDENTITY(1,1) NOT NULL,
    role_code   VARCHAR(30)   NOT NULL,
    role_name   NVARCHAR(100) NOT NULL,
    description NVARCHAR(300) NULL,
    is_system   BIT           NOT NULL CONSTRAINT df_tbl_roles_is_system DEFAULT (0),
    is_active   BIT           NOT NULL CONSTRAINT df_tbl_roles_is_active DEFAULT (1),
    created_at  DATETIME2(0)  NOT NULL CONSTRAINT df_tbl_roles_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_tbl_roles PRIMARY KEY (id),
    CONSTRAINT uq_tbl_roles_role_code UNIQUE (role_code)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_permissions ---------- */
IF OBJECT_ID(N'dbo.tbl_permissions', N'U') IS NULL
CREATE TABLE tbl_permissions (
    id               INT           IDENTITY(1,1) NOT NULL,
    permission_code  VARCHAR(60)   NOT NULL,        -- VD incident.dispatch
    permission_name  NVARCHAR(150) NOT NULL,
    module           VARCHAR(30)   NOT NULL,
    CONSTRAINT pk_tbl_permissions PRIMARY KEY (id),
    CONSTRAINT uq_tbl_permissions_permission_code UNIQUE (permission_code)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_role_permissions ---------- */
IF OBJECT_ID(N'dbo.tbl_role_permissions', N'U') IS NULL
CREATE TABLE tbl_role_permissions (
    role_id        INT NOT NULL,
    permission_id  INT NOT NULL,
    CONSTRAINT pk_tbl_role_permissions PRIMARY KEY (role_id, permission_id),
    CONSTRAINT fk_tbl_role_permissions_role_id FOREIGN KEY (role_id) REFERENCES tbl_roles (id) ON DELETE CASCADE,
    CONSTRAINT fk_tbl_role_permissions_permission_id FOREIGN KEY (permission_id) REFERENCES tbl_permissions (id) ON DELETE CASCADE
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_role_permissions_permission_id' AND object_id = OBJECT_ID(N'dbo.tbl_role_permissions'))
    CREATE INDEX ix_tbl_role_permissions_permission_id ON tbl_role_permissions (permission_id);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_users ---------- */
IF OBJECT_ID(N'dbo.tbl_users', N'U') IS NULL
CREATE TABLE tbl_users (
    id                  INT              IDENTITY(1,1) NOT NULL,
    username            VARCHAR(50)      NULL,      -- NULL voi cong dan an danh
    password_hash       VARCHAR(255)     NULL,
    full_name           NVARCHAR(150)    NOT NULL,
    phone               VARCHAR(15)      NULL,
    email               VARCHAR(150)     NULL,
    is_active           BIT              NOT NULL CONSTRAINT df_tbl_users_is_active DEFAULT (1),
    security_stamp      UNIQUEIDENTIFIER NOT NULL CONSTRAINT df_tbl_users_security_stamp DEFAULT (NEWID()),
    failed_login_count  TINYINT          NOT NULL CONSTRAINT df_tbl_users_failed_login DEFAULT (0),
    locked_until        DATETIME2(0)     NULL,
    last_login_at       DATETIME2(0)     NULL,
    created_at          DATETIME2(0)     NOT NULL CONSTRAINT df_tbl_users_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at          DATETIME2(0)     NULL,
    CONSTRAINT pk_tbl_users PRIMARY KEY (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'uq_tbl_users_username' AND object_id = OBJECT_ID(N'dbo.tbl_users'))
    CREATE UNIQUE INDEX uq_tbl_users_username ON tbl_users (username) WHERE username IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_users_phone' AND object_id = OBJECT_ID(N'dbo.tbl_users'))
    CREATE INDEX ix_tbl_users_phone ON tbl_users (phone) WHERE phone IS NOT NULL;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_user_roles (vai tro theo pham vi don vi) ---------- */
IF OBJECT_ID(N'dbo.tbl_user_roles', N'U') IS NULL
CREATE TABLE tbl_user_roles (
    id                   INT           IDENTITY(1,1) NOT NULL,
    user_id              INT           NOT NULL,
    role_id              INT           NOT NULL,
    admin_unit_id        INT           NULL,        -- NULL = toan he thong
    assigned_by_user_id  INT           NULL,
    assigned_at          DATETIME2(0)  NOT NULL CONSTRAINT df_tbl_user_roles_assigned_at DEFAULT (SYSUTCDATETIME()),
    expires_at           DATETIME2(0)  NULL,
    is_active            BIT           NOT NULL CONSTRAINT df_tbl_user_roles_is_active DEFAULT (1),
    CONSTRAINT pk_tbl_user_roles PRIMARY KEY (id),
    CONSTRAINT uq_tbl_user_roles_scope UNIQUE (user_id, role_id, admin_unit_id),
    CONSTRAINT fk_tbl_user_roles_user_id FOREIGN KEY (user_id) REFERENCES tbl_users (id),
    CONSTRAINT fk_tbl_user_roles_role_id FOREIGN KEY (role_id) REFERENCES tbl_roles (id),
    CONSTRAINT fk_tbl_user_roles_admin_unit_id FOREIGN KEY (admin_unit_id) REFERENCES tbl_admin_units (id),
    CONSTRAINT fk_tbl_user_roles_assigned_by FOREIGN KEY (assigned_by_user_id) REFERENCES tbl_users (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_user_roles_admin_unit_id' AND object_id = OBJECT_ID(N'dbo.tbl_user_roles'))
    CREATE INDEX ix_tbl_user_roles_admin_unit_id ON tbl_user_roles (admin_unit_id, role_id) WHERE is_active = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_user_roles_role_id' AND object_id = OBJECT_ID(N'dbo.tbl_user_roles'))
    CREATE INDEX ix_tbl_user_roles_role_id ON tbl_user_roles (role_id);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_refresh_tokens (chi luu hash SHA-256) ---------- */
IF OBJECT_ID(N'dbo.tbl_refresh_tokens', N'U') IS NULL
CREATE TABLE tbl_refresh_tokens (
    id                    BIGINT           IDENTITY(1,1) NOT NULL,
    user_id               INT              NOT NULL,
    token_hash            CHAR(64)         NOT NULL,
    expires_at            DATETIME2(0)     NOT NULL,
    created_at            DATETIME2(0)     NOT NULL CONSTRAINT df_tbl_refresh_tokens_created_at DEFAULT (SYSUTCDATETIME()),
    created_by_ip         VARCHAR(45)      NULL,
    revoked_at            DATETIME2(0)     NULL,
    replaced_by_token_id  BIGINT           NULL,
    security_stamp        UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT pk_tbl_refresh_tokens PRIMARY KEY (id),
    CONSTRAINT uq_tbl_refresh_tokens_token_hash UNIQUE (token_hash),
    CONSTRAINT fk_tbl_refresh_tokens_user_id FOREIGN KEY (user_id) REFERENCES tbl_users (id),
    CONSTRAINT fk_tbl_refresh_tokens_replaced_by FOREIGN KEY (replaced_by_token_id) REFERENCES tbl_refresh_tokens (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_refresh_tokens_user_id' AND object_id = OBJECT_ID(N'dbo.tbl_refresh_tokens'))
    CREATE INDEX ix_tbl_refresh_tokens_user_id ON tbl_refresh_tokens (user_id) WHERE revoked_at IS NULL;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_categories ---------- */
IF OBJECT_ID(N'dbo.tbl_categories', N'U') IS NULL
CREATE TABLE tbl_categories (
    id                  INT           IDENTITY(1,1) NOT NULL,
    category_code       VARCHAR(30)   NOT NULL,
    category_name       NVARCHAR(100) NOT NULL,
    default_priority    TINYINT       NOT NULL,     -- 1 thap, 2 trung binh, 3 khan cap
    is_active           BIT           NOT NULL CONSTRAINT df_tbl_categories_is_active DEFAULT (1),
    CONSTRAINT pk_tbl_categories PRIMARY KEY (id),
    CONSTRAINT uq_tbl_categories_category_code UNIQUE (category_code),
    CONSTRAINT ck_tbl_categories_default_priority CHECK (default_priority IN (1, 2, 3))
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_sla_policies ---------- */
IF OBJECT_ID(N'dbo.tbl_sla_policies', N'U') IS NULL
CREATE TABLE tbl_sla_policies (
    id                 INT           IDENTITY(1,1) NOT NULL,
    priority           TINYINT       NOT NULL,
    sla_level          TINYINT       NOT NULL,
    sla_hours          SMALLINT      NOT NULL,
    escalation_percent TINYINT       NOT NULL CONSTRAINT df_tbl_sla_policies_escalation DEFAULT (80),
    description        NVARCHAR(200) NULL,
    CONSTRAINT pk_tbl_sla_policies PRIMARY KEY (id),
    CONSTRAINT uq_tbl_sla_policies_priority UNIQUE (priority),
    CONSTRAINT ck_tbl_sla_policies_priority CHECK (priority IN (1, 2, 3)),
    CONSTRAINT ck_tbl_sla_policies_sla_hours CHECK (sla_hours > 0),
    CONSTRAINT ck_tbl_sla_policies_escalation CHECK (escalation_percent BETWEEN 1 AND 100)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_grids ---------- */
IF OBJECT_ID(N'dbo.tbl_grids', N'U') IS NULL
CREATE TABLE tbl_grids (
    id                  INT           IDENTITY(1,1) NOT NULL,
    grid_code           VARCHAR(50)   NOT NULL,
    name                NVARCHAR(150) NOT NULL,
    admin_unit_id       INT           NOT NULL,     -- don vi thap nhat chua o luoi
    boundary            GEOGRAPHY     NOT NULL,     -- Polygon SRID 4326
    assigned_officer_id INT           NULL,
    is_active           BIT           NOT NULL CONSTRAINT df_tbl_grids_is_active DEFAULT (1),
    created_at          DATETIME2(0)  NOT NULL CONSTRAINT df_tbl_grids_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at          DATETIME2(0)  NULL,
    CONSTRAINT pk_tbl_grids PRIMARY KEY (id),
    CONSTRAINT uq_tbl_grids_grid_code UNIQUE (grid_code),
    CONSTRAINT fk_tbl_grids_assigned_officer_id FOREIGN KEY (assigned_officer_id) REFERENCES tbl_users (id),
    CONSTRAINT fk_tbl_grids_admin_unit_id FOREIGN KEY (admin_unit_id) REFERENCES tbl_admin_units (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'six_tbl_grids_boundary' AND object_id = OBJECT_ID(N'dbo.tbl_grids'))
    CREATE SPATIAL INDEX six_tbl_grids_boundary ON tbl_grids (boundary) USING GEOGRAPHY_AUTO_GRID;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_grids_admin_unit_id' AND object_id = OBJECT_ID(N'dbo.tbl_grids'))
    CREATE INDEX ix_tbl_grids_admin_unit_id ON tbl_grids (admin_unit_id);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_grid_officers ---------- */
IF OBJECT_ID(N'dbo.tbl_grid_officers', N'U') IS NULL
CREATE TABLE tbl_grid_officers (
    grid_id     INT          NOT NULL,
    user_id     INT          NOT NULL,
    is_primary  BIT          NOT NULL CONSTRAINT df_tbl_grid_officers_is_primary DEFAULT (0),
    assigned_at DATETIME2(0) NOT NULL CONSTRAINT df_tbl_grid_officers_assigned_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_tbl_grid_officers PRIMARY KEY (grid_id, user_id),
    CONSTRAINT fk_tbl_grid_officers_grid_id FOREIGN KEY (grid_id) REFERENCES tbl_grids (id),
    CONSTRAINT fk_tbl_grid_officers_user_id FOREIGN KEY (user_id) REFERENCES tbl_users (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'uq_tbl_grid_officers_primary' AND object_id = OBJECT_ID(N'dbo.tbl_grid_officers'))
    CREATE UNIQUE INDEX uq_tbl_grid_officers_primary ON tbl_grid_officers (grid_id) WHERE is_primary = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_grid_officers_user_id' AND object_id = OBJECT_ID(N'dbo.tbl_grid_officers'))
    CREATE INDEX ix_tbl_grid_officers_user_id ON tbl_grid_officers (user_id);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_incidents ---------- */
IF OBJECT_ID(N'dbo.tbl_incidents', N'U') IS NULL
CREATE TABLE tbl_incidents (
    id                   BIGINT         IDENTITY(1,1) NOT NULL,
    tracking_code        VARCHAR(20)    NOT NULL,
    reporter_user_id     INT            NULL,
    reporter_phone       VARCHAR(15)    NULL,
    description          NVARCHAR(1000) NULL,
    category_id          INT            NULL,
    ai_label             NVARCHAR(100)  NULL,
    ai_confidence        DECIMAL(5,4)   NULL,
    is_manual_review     BIT            NOT NULL CONSTRAINT df_tbl_incidents_manual DEFAULT (0),
    location_point       GEOGRAPHY      NOT NULL,   -- Point SRID 4326
    address_text         NVARCHAR(255)  NULL,
    grid_id              INT            NULL,
    admin_unit_id        INT            NULL,
    assigned_officer_id  INT            NULL,
    parent_incident_id   BIGINT         NULL,
    duplicate_count      INT            NOT NULL CONSTRAINT df_tbl_incidents_dup DEFAULT (0),
    status               TINYINT        NOT NULL CONSTRAINT df_tbl_incidents_status DEFAULT (0),
                                         -- 0 moi, 1 da giao, 2 dang xu ly, 3 hoan thanh, 4 huy
    priority             TINYINT        NOT NULL CONSTRAINT df_tbl_incidents_priority DEFAULT (2),
    sla_policy_id        INT            NULL,
    sla_deadline         DATETIME2(0)   NULL,
    accepted_at          DATETIME2(0)   NULL,
    is_overdue           BIT            NOT NULL CONSTRAINT df_tbl_incidents_overdue DEFAULT (0),
    escalated_at         DATETIME2(0)   NULL,
    resolved_at          DATETIME2(0)   NULL,
    created_at           DATETIME2(0)   NOT NULL CONSTRAINT df_tbl_incidents_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at           DATETIME2(0)   NULL,
    processed_at         DATETIME2(0)   NULL,       -- da di het pipeline nen
    CONSTRAINT pk_tbl_incidents PRIMARY KEY (id),
    CONSTRAINT uq_tbl_incidents_tracking_code UNIQUE (tracking_code),
    CONSTRAINT fk_tbl_incidents_reporter_user_id FOREIGN KEY (reporter_user_id) REFERENCES tbl_users (id),
    CONSTRAINT fk_tbl_incidents_category_id FOREIGN KEY (category_id) REFERENCES tbl_categories (id),
    CONSTRAINT fk_tbl_incidents_grid_id FOREIGN KEY (grid_id) REFERENCES tbl_grids (id),
    CONSTRAINT fk_tbl_incidents_admin_unit_id FOREIGN KEY (admin_unit_id) REFERENCES tbl_admin_units (id),
    CONSTRAINT fk_tbl_incidents_assigned_officer_id FOREIGN KEY (assigned_officer_id) REFERENCES tbl_users (id),
    CONSTRAINT fk_tbl_incidents_parent_incident_id FOREIGN KEY (parent_incident_id) REFERENCES tbl_incidents (id),
    CONSTRAINT fk_tbl_incidents_sla_policy_id FOREIGN KEY (sla_policy_id) REFERENCES tbl_sla_policies (id),
    CONSTRAINT ck_tbl_incidents_status CHECK (status IN (0, 1, 2, 3, 4)),
    CONSTRAINT ck_tbl_incidents_priority CHECK (priority IN (1, 2, 3)),
    CONSTRAINT ck_tbl_incidents_ai_confidence CHECK (ai_confidence IS NULL OR ai_confidence BETWEEN 0 AND 1),
    CONSTRAINT ck_tbl_incidents_not_own_parent CHECK (parent_incident_id IS NULL OR parent_incident_id <> id),
    CONSTRAINT ck_tbl_incidents_resolved CHECK (status <> 3 OR resolved_at IS NOT NULL)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
-- Database tao tu 01_schema.sql cu (chua chay 04) thi bo sung cot
IF COL_LENGTH(N'dbo.tbl_incidents', N'processed_at') IS NULL
    ALTER TABLE tbl_incidents ADD processed_at DATETIME2(0) NULL;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'six_tbl_incidents_location_point' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE SPATIAL INDEX six_tbl_incidents_location_point ON tbl_incidents (location_point) USING GEOGRAPHY_AUTO_GRID;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_grid_status' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_grid_status ON tbl_incidents (grid_id, status) INCLUDE (priority, sla_deadline, is_overdue);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_officer_status' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_officer_status ON tbl_incidents (assigned_officer_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_admin_unit' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_admin_unit ON tbl_incidents (admin_unit_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_parent' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_parent ON tbl_incidents (parent_incident_id) WHERE parent_incident_id IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_reporter_phone' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_reporter_phone ON tbl_incidents (reporter_phone) WHERE reporter_phone IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_created_at' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_created_at ON tbl_incidents (created_at);
-- SlaEngine quet cac su co dang mo theo han SLA
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_sla_scan' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_sla_scan ON tbl_incidents (sla_deadline) INCLUDE (status, is_overdue, escalated_at, created_at)
        WHERE status IN (0, 1, 2) AND sla_deadline IS NOT NULL;
-- Tim ticket cha cung danh muc, trong 24h
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_dedup' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_dedup ON tbl_incidents (category_id, created_at) WHERE parent_incident_id IS NULL;
-- Pipeline nen: su co chua xu ly xong
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incidents_pending_pipeline' AND object_id = OBJECT_ID(N'dbo.tbl_incidents'))
    CREATE INDEX ix_tbl_incidents_pending_pipeline ON tbl_incidents (created_at) WHERE processed_at IS NULL;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_incident_images (Before / After) ---------- */
IF OBJECT_ID(N'dbo.tbl_incident_images', N'U') IS NULL
CREATE TABLE tbl_incident_images (
    id                  BIGINT         IDENTITY(1,1) NOT NULL,
    incident_id         BIGINT         NOT NULL,
    image_type          VARCHAR(10)    NOT NULL,      -- BEFORE | AFTER
    image_url           NVARCHAR(500)  NOT NULL,
    capture_point       GEOGRAPHY      NULL,
    uploaded_by_user_id INT            NULL,
    uploaded_at         DATETIME2(0)   NOT NULL CONSTRAINT df_tbl_incident_images_uploaded_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_tbl_incident_images PRIMARY KEY (id),
    CONSTRAINT fk_tbl_incident_images_incident_id FOREIGN KEY (incident_id) REFERENCES tbl_incidents (id),
    CONSTRAINT fk_tbl_incident_images_uploaded_by FOREIGN KEY (uploaded_by_user_id) REFERENCES tbl_users (id),
    CONSTRAINT ck_tbl_incident_images_type CHECK (image_type IN ('BEFORE', 'AFTER'))
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incident_images_incident_id' AND object_id = OBJECT_ID(N'dbo.tbl_incident_images'))
    CREATE INDEX ix_tbl_incident_images_incident_id ON tbl_incident_images (incident_id, image_type);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_incident_logs ---------- */
IF OBJECT_ID(N'dbo.tbl_incident_logs', N'U') IS NULL
CREATE TABLE tbl_incident_logs (
    id                  BIGINT        IDENTITY(1,1) NOT NULL,
    incident_id         BIGINT        NOT NULL,
    action_by_user_id   INT           NULL,         -- NULL neu he thong tu dong
    action_type         VARCHAR(30)   NOT NULL,
    action_note         NVARCHAR(500) NOT NULL,
    old_status          TINYINT       NULL,
    new_status          TINYINT       NULL,
    created_at          DATETIME2(0)  NOT NULL CONSTRAINT df_tbl_incident_logs_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_tbl_incident_logs PRIMARY KEY (id),
    CONSTRAINT fk_tbl_incident_logs_incident_id FOREIGN KEY (incident_id) REFERENCES tbl_incidents (id),
    CONSTRAINT fk_tbl_incident_logs_action_by_user_id FOREIGN KEY (action_by_user_id) REFERENCES tbl_users (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_incident_logs_incident_id' AND object_id = OBJECT_ID(N'dbo.tbl_incident_logs'))
    CREATE INDEX ix_tbl_incident_logs_incident_id ON tbl_incident_logs (incident_id, created_at);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_incident_ratings ---------- */
IF OBJECT_ID(N'dbo.tbl_incident_ratings', N'U') IS NULL
CREATE TABLE tbl_incident_ratings (
    id           BIGINT        IDENTITY(1,1) NOT NULL,
    incident_id  BIGINT        NOT NULL,
    rating       TINYINT       NOT NULL,
    feedback     NVARCHAR(500) NULL,
    created_at   DATETIME2(0)  NOT NULL CONSTRAINT df_tbl_incident_ratings_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT pk_tbl_incident_ratings PRIMARY KEY (id),
    CONSTRAINT uq_tbl_incident_ratings_incident_id UNIQUE (incident_id),
    CONSTRAINT fk_tbl_incident_ratings_incident_id FOREIGN KEY (incident_id) REFERENCES tbl_incidents (id),
    CONSTRAINT ck_tbl_incident_ratings_rating CHECK (rating BETWEEN 1 AND 5)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- tbl_notifications ---------- */
IF OBJECT_ID(N'dbo.tbl_notifications', N'U') IS NULL
CREATE TABLE tbl_notifications (
    id                 BIGINT        IDENTITY(1,1) NOT NULL,
    user_id            INT           NOT NULL,
    incident_id        BIGINT        NULL,
    notification_type  VARCHAR(30)   NOT NULL,      -- NEW_ASSIGNMENT, SLA_WARNING, ESCALATION, OVERDUE, MANUAL_REVIEW
    title              NVARCHAR(200) NOT NULL,
    content            NVARCHAR(500) NULL,
    is_read            BIT           NOT NULL CONSTRAINT df_tbl_notifications_is_read DEFAULT (0),
    created_at         DATETIME2(0)  NOT NULL CONSTRAINT df_tbl_notifications_created_at DEFAULT (SYSUTCDATETIME()),
    read_at            DATETIME2(0)  NULL,
    CONSTRAINT pk_tbl_notifications PRIMARY KEY (id),
    CONSTRAINT fk_tbl_notifications_user_id FOREIGN KEY (user_id) REFERENCES tbl_users (id),
    CONSTRAINT fk_tbl_notifications_incident_id FOREIGN KEY (incident_id) REFERENCES tbl_incidents (id)
);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_tbl_notifications_user_unread' AND object_id = OBJECT_ID(N'dbo.tbl_notifications'))
    CREATE INDEX ix_tbl_notifications_user_unread ON tbl_notifications (user_id, is_read, created_at DESC);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   3. Trigger cay hanh chinh (nguyen van tu 01_schema.sql)
   ===================================================================== */

CREATE OR ALTER TRIGGER trg_tbl_admin_units_path
ON tbl_admin_units
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(parent_id) AND NOT UPDATE(unit_type_id) RETURN;

    -- Don vi con phai o cap thap hon don vi cha
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN tbl_admin_unit_types ct ON ct.id = i.unit_type_id
        JOIN tbl_admin_levels     cl ON cl.id = ct.level_id
        JOIN tbl_admin_units      p  ON p.id  = i.parent_id
        JOIN tbl_admin_unit_types pt ON pt.id = p.unit_type_id
        JOIN tbl_admin_levels     pl ON pl.id = pt.level_id
        WHERE cl.level_order <= pl.level_order)
    BEGIN
        RAISERROR (N'Đơn vị con phải thuộc cấp thấp hơn đơn vị cha.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    -- Khong duoc chuyen mot don vi vao ben duoi chinh cay con cua no
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN tbl_admin_units p ON p.id = i.parent_id
        WHERE p.unit_path LIKE '%/' + CAST(i.id AS VARCHAR(10)) + '/%')
    BEGIN
        RAISERROR (N'Không thể gán đơn vị cha nằm trong cây con của chính nó.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    -- Tinh lai path tu cac goc bi thay doi (bo qua dong co cha cung nam trong lo nay)
    WITH tree AS (
        SELECT u.id,
               CAST(ISNULL(p.unit_path, '/') + CAST(u.id AS VARCHAR(10)) + '/' AS VARCHAR(400)) AS new_path
        FROM inserted i
        JOIN tbl_admin_units u ON u.id = i.id
        LEFT JOIN tbl_admin_units p ON p.id = u.parent_id
        WHERE NOT EXISTS (SELECT 1 FROM inserted i2 WHERE i2.id = u.parent_id)
        UNION ALL
        SELECT c.id, CAST(t.new_path + CAST(c.id AS VARCHAR(10)) + '/' AS VARCHAR(400))
        FROM tbl_admin_units c
        JOIN tree t ON c.parent_id = t.id
    )
    UPDATE u
    SET unit_path = t.new_path
    FROM tbl_admin_units u
    JOIN tree t ON t.id = u.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO


/* =====================================================================
   4. View, function, proc khong gian va RBAC (nguyen van 03_spatial_procedures.sql)
   ===================================================================== */

/* =====================================================================
   Ham / thu tuc / view dung chung cho backend va bao cao
   ===================================================================== */
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* ---------- Cay hanh chinh: moi don vi kem tat ca to tien (ke ca chinh no) ----------
   Dung de bao cao theo bat ky cap nao: JOIN qua ancestor_id va loc level_code. */
CREATE OR ALTER VIEW vw_admin_unit_ancestors
AS
SELECT
    u.id            AS unit_id,
    a.id            AS ancestor_id,
    a.unit_code     AS ancestor_code,
    a.name          AS ancestor_name,
    l.level_code    AS ancestor_level_code,
    l.level_order   AS ancestor_level_order
FROM tbl_admin_units u
JOIN tbl_admin_units a       ON u.unit_path LIKE a.unit_path + '%'
JOIN tbl_admin_unit_types t  ON t.id = a.unit_type_id
JOIN tbl_admin_levels l      ON l.id = t.level_id;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* UC-04: tim o luoi chua mot toa do */
CREATE OR ALTER PROCEDURE sp_find_grid_by_point
    @latitude  FLOAT,
    @longitude FLOAT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @point GEOGRAPHY = geography::Point(@latitude, @longitude, 4326);

    SELECT TOP (1) id, grid_code, name, admin_unit_id, assigned_officer_id
    FROM tbl_grids
    WHERE is_active = 1 AND boundary.STContains(@point) = 1
    ORDER BY id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* UC-04 (du phong): diem nam ngoai moi o luoi thi tim don vi hanh chinh thap nhat chua diem,
   de chuyen dieu phoi vien dung phuong/xa. */
CREATE OR ALTER PROCEDURE sp_find_admin_unit_by_point
    @latitude  FLOAT,
    @longitude FLOAT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @point GEOGRAPHY = geography::Point(@latitude, @longitude, 4326);

    SELECT TOP (1) u.id, u.unit_code, u.name, l.level_code, l.level_order, u.unit_path
    FROM tbl_admin_units u
    JOIN tbl_admin_unit_types t ON t.id = u.unit_type_id
    JOIN tbl_admin_levels l     ON l.id = t.level_id
    WHERE u.is_active = 1
      AND u.boundary IS NOT NULL
      AND u.boundary.STContains(@point) = 1
    ORDER BY l.level_order DESC;          -- uu tien cap thap nhat (khu pho/thon)
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* UC-05: tim ticket cha trong ban kinh @radius_m, cung danh muc, trong @window_hours gio */
CREATE OR ALTER PROCEDURE sp_find_parent_incident
    @latitude     FLOAT,
    @longitude    FLOAT,
    @category_id  INT,
    @radius_m     FLOAT = 20,
    @window_hours INT   = 24,
    @before_id    BIGINT = NULL   -- chi tim ticket cha co id NHO HON (tranh vong cha-con)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @point GEOGRAPHY = geography::Point(@latitude, @longitude, 4326);

    SELECT TOP (1) id, tracking_code, location_point.STDistance(@point) AS distance_m
    FROM tbl_incidents
    WHERE parent_incident_id IS NULL
      AND status IN (0, 1, 2)
      AND category_id = @category_id
      AND created_at >= DATEADD(HOUR, -@window_hours, SYSUTCDATETIME())
      AND (@before_id IS NULL OR id < @before_id)
      AND location_point.STDistance(@point) <= @radius_m
    ORDER BY location_point.STDistance(@point);
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   RBAC CO PHAM VI
   Quy tac: nguoi dung co quyen P tren don vi X khi ho giu mot vai tro co P,
   duoc gan tai don vi A, va A la X hoac to tien cua X (hoac A = NULL: toan he thong).
   ===================================================================== */

/* Cac (quyen, pham vi) dang co hieu luc cua nguoi dung. Dung de nap claim vao JWT. */
CREATE OR ALTER FUNCTION fn_user_permission_scopes (@user_id INT)
RETURNS TABLE
AS
RETURN
    SELECT DISTINCT
        p.permission_code,
        r.role_code,
        ur.admin_unit_id        AS scope_unit_id,
        au.unit_path            AS scope_unit_path       -- NULL = toan he thong
    FROM tbl_users usr
    JOIN tbl_user_roles ur        ON ur.user_id = usr.id
    JOIN tbl_roles r              ON r.id = ur.role_id
    JOIN tbl_role_permissions rp  ON rp.role_id = r.id
    JOIN tbl_permissions p        ON p.id = rp.permission_id
    LEFT JOIN tbl_admin_units au  ON au.id = ur.admin_unit_id
    WHERE usr.id = @user_id
      AND usr.is_active = 1
      AND ur.is_active = 1
      AND r.is_active = 1
      AND (ur.expires_at IS NULL OR ur.expires_at > SYSUTCDATETIME());
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* Kiem tra mot quyen tren mot don vi cu the.
   @target_unit_id = NULL: thao tac cap he thong, chi pham vi toan he thong moi duoc. */
CREATE OR ALTER FUNCTION fn_has_permission
(
    @user_id         INT,
    @permission_code VARCHAR(60),
    @target_unit_id  INT
)
RETURNS BIT
AS
BEGIN
    DECLARE @target_path VARCHAR(400) = (SELECT unit_path FROM tbl_admin_units WHERE id = @target_unit_id);

    IF EXISTS (
        SELECT 1
        FROM fn_user_permission_scopes(@user_id) s
        WHERE s.permission_code = @permission_code
          AND (s.scope_unit_id IS NULL
               OR (@target_path IS NOT NULL AND @target_path LIKE s.scope_unit_path + '%')))
        RETURN 1;

    RETURN 0;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* Tat ca don vi nguoi dung duoc thao tac voi mot quyen. Dung de loc danh sach su co, o luoi, bao cao. */
CREATE OR ALTER FUNCTION fn_user_accessible_units
(
    @user_id         INT,
    @permission_code VARCHAR(60)
)
RETURNS TABLE
AS
RETURN
    SELECT u.id AS admin_unit_id
    FROM tbl_admin_units u
    WHERE EXISTS (
        SELECT 1
        FROM fn_user_permission_scopes(@user_id) s
        WHERE s.permission_code = @permission_code
          AND (s.scope_unit_id IS NULL OR u.unit_path LIKE s.scope_unit_path + '%'));
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* Nap quyen khi dang nhap (backend dua vao JWT hoac cache) */
CREATE OR ALTER PROCEDURE sp_get_user_permissions
    @user_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT permission_code, role_code, scope_unit_id, scope_unit_path
    FROM fn_user_permission_scopes(@user_id)
    ORDER BY permission_code, scope_unit_path;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* Su co nguoi dung duoc xem (vi du cho man hinh danh sach) */
CREATE OR ALTER FUNCTION fn_user_visible_incidents (@user_id INT)
RETURNS TABLE
AS
RETURN
    SELECT i.id
    FROM tbl_incidents i
    WHERE i.admin_unit_id IN (SELECT admin_unit_id FROM fn_user_accessible_units(@user_id, 'incident.view'))
       OR EXISTS (SELECT 1 FROM fn_user_permission_scopes(@user_id) s
                  WHERE s.permission_code = 'incident.view' AND s.scope_unit_id IS NULL)
       OR (i.reporter_user_id = @user_id
           AND EXISTS (SELECT 1 FROM fn_user_permission_scopes(@user_id) s
                       WHERE s.permission_code = 'incident.view_own'));
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* UC-06: tim nguoi nhan leo thang theo cay hanh chinh.
   Tra ve nguoi co quyen incident.receive_escalation duoc gan tai to tien cua don vi chua su co,
   gan nhat truoc. @level_code = NULL lay tat ca cap; 'COMMUNE' chi lay cap phuong/xa. */
CREATE OR ALTER PROCEDURE sp_find_escalation_targets
    @incident_id     BIGINT,
    @level_code      VARCHAR(30) = NULL,
    @permission_code VARCHAR(60) = 'incident.receive_escalation'
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @unit_id INT = (
        SELECT COALESCE(i.admin_unit_id, g.admin_unit_id)
        FROM tbl_incidents i
        LEFT JOIN tbl_grids g ON g.id = i.grid_id
        WHERE i.id = @incident_id);

    SELECT DISTINCT
           usr.id AS user_id, usr.full_name, usr.phone,
           anc.ancestor_id AS scope_unit_id, anc.ancestor_name AS scope_unit_name,
           anc.ancestor_level_code, anc.ancestor_level_order
    FROM vw_admin_unit_ancestors anc
    JOIN tbl_user_roles ur       ON ur.admin_unit_id = anc.ancestor_id
                                AND ur.is_active = 1
                                AND (ur.expires_at IS NULL OR ur.expires_at > SYSUTCDATETIME())
    JOIN tbl_users usr           ON usr.id = ur.user_id AND usr.is_active = 1
    JOIN tbl_roles r             ON r.id = ur.role_id AND r.is_active = 1
    JOIN tbl_role_permissions rp ON rp.role_id = r.id
    JOIN tbl_permissions p       ON p.id = rp.permission_id AND p.permission_code = @permission_code
    WHERE anc.unit_id = @unit_id
      AND (@level_code IS NULL OR anc.ancestor_level_code = @level_code)
    ORDER BY anc.ancestor_level_order DESC;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* UC-07: anh After co nam trong o luoi cua su co khong (cho phep sai so @tolerance_m) */
CREATE OR ALTER FUNCTION fn_is_point_in_incident_grid
(
    @incident_id BIGINT,
    @latitude    FLOAT,
    @longitude   FLOAT,
    @tolerance_m FLOAT
)
RETURNS BIT
AS
BEGIN
    DECLARE @point GEOGRAPHY = geography::Point(@latitude, @longitude, 4326);
    DECLARE @result BIT = 0;

    SELECT @result = CASE WHEN g.boundary.STDistance(@point) <= @tolerance_m THEN 1 ELSE 0 END
    FROM tbl_incidents i
    JOIN tbl_grids g ON g.id = i.grid_id
    WHERE i.id = @incident_id;

    RETURN @result;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* UC-08: trang thai mau cua tung o luoi (GREEN / YELLOW / RED), kem ten khu pho va phuong/xa */
CREATE OR ALTER VIEW vw_grid_status
AS
SELECT
    g.id                AS grid_id,
    g.grid_code,
    g.name,
    g.admin_unit_id,
    hamlet.name         AS hamlet_name,
    commune.ancestor_id   AS commune_id,
    commune.ancestor_name AS commune_name,
    g.boundary,
    s.open_incident_count,
    s.overdue_incident_count,
    CASE
        WHEN s.overdue_incident_count > 0 THEN 'RED'
        WHEN s.open_incident_count > 0 THEN 'YELLOW'
        ELSE 'GREEN'
    END                 AS status_color
FROM tbl_grids g
JOIN tbl_admin_units hamlet ON hamlet.id = g.admin_unit_id
LEFT JOIN vw_admin_unit_ancestors commune
       ON commune.unit_id = g.admin_unit_id AND commune.ancestor_level_code = 'COMMUNE'
OUTER APPLY (
    -- geography khong the nam trong GROUP BY nen gom so lieu o day
    SELECT COUNT(*)                                          AS open_incident_count,
           ISNULL(SUM(CASE WHEN i.is_overdue = 1 THEN 1 ELSE 0 END), 0) AS overdue_incident_count
    FROM tbl_incidents i
    WHERE i.grid_id = g.id AND i.status IN (0, 1, 2) AND i.parent_incident_id IS NULL
) s
WHERE g.is_active = 1;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* Bao cao: thong ke su co theo cac don vi con truc tiep cua @unit_id
   VD: @unit_id = tinh -> tung phuong/xa; @unit_id = phuong -> tung khu pho. */
CREATE OR ALTER PROCEDURE sp_incident_summary_by_child_units
    @unit_id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.id AS unit_id, c.unit_code, c.name,
           COUNT(i.id)                                              AS total_incidents,
           SUM(CASE WHEN i.status IN (0, 1, 2) THEN 1 ELSE 0 END)   AS open_incidents,
           SUM(CASE WHEN i.status = 3 THEN 1 ELSE 0 END)            AS resolved_incidents,
           SUM(CASE WHEN i.is_overdue = 1 THEN 1 ELSE 0 END)        AS overdue_incidents
    FROM tbl_admin_units c
    LEFT JOIN tbl_admin_units leaf ON leaf.unit_path LIKE c.unit_path + '%'
    LEFT JOIN tbl_incidents i      ON i.admin_unit_id = leaf.id AND i.parent_incident_id IS NULL
    WHERE c.parent_id = @unit_id AND c.is_active = 1
    GROUP BY c.id, c.unit_code, c.name
    ORDER BY c.name;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO


/* =====================================================================
   5. Stored procedure backend (nguyen van 05_procedures.sql)
   ===================================================================== */

/* =====================================================================
   05 - Stored procedure cho backend GridSmart (theo docs/proc-contract.md)
   - CRUD chuan cho 18 bang: SP_tbl_<bang>_Insert / _Update / _Delete / _ReadAll / _ReadById
   - Proc nghiep vu: SP_Auth_*, SP_tbl_*_<Muc dich>, SP_rpt_*
   - Chay lai nhieu lan an toan (CREATE OR ALTER). Khong doi bang, khong xoa du lieu.
   ===================================================================== */
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   A. CRUD: tbl_admin_levels (khoa TINYINT, khong identity: Insert nhan @id)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_admin_levels_Insert
    @id          INT,
    @level_code  VARCHAR(30),
    @level_name  NVARCHAR(100),
    @level_order TINYINT,
    @new_id      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_admin_levels (id, level_code, level_name, level_order)
    VALUES (@id, @level_code, @level_name, @level_order);
    SET @new_id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_levels_Update
    @id          INT,
    @level_code  VARCHAR(30),
    @level_name  NVARCHAR(100),
    @level_order TINYINT
AS
BEGIN
    UPDATE tbl_admin_levels
    SET level_code = @level_code, level_name = @level_name, level_order = @level_order
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_levels_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_admin_levels WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_levels_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, level_code, level_name, level_order FROM tbl_admin_levels;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_levels_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, level_code, level_name, level_order FROM tbl_admin_levels WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   B. CRUD: tbl_admin_unit_types (khoa TINYINT, khong identity)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_admin_unit_types_Insert
    @id        INT,
    @type_code VARCHAR(30),
    @type_name NVARCHAR(100),
    @level_id  TINYINT,
    @new_id    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_admin_unit_types (id, type_code, type_name, level_id)
    VALUES (@id, @type_code, @type_name, @level_id);
    SET @new_id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_unit_types_Update
    @id        INT,
    @type_code VARCHAR(30),
    @type_name NVARCHAR(100),
    @level_id  TINYINT
AS
BEGIN
    UPDATE tbl_admin_unit_types
    SET type_code = @type_code, type_name = @type_name, level_id = @level_id
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_unit_types_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_admin_unit_types WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_unit_types_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, type_code, type_name, level_id FROM tbl_admin_unit_types;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_unit_types_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, type_code, type_name, level_id FROM tbl_admin_unit_types WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   C. CRUD: tbl_admin_units (unit_path do trigger tinh, khong nam trong Insert/Update)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_admin_units_Insert
    @unit_code           VARCHAR(30),
    @name                NVARCHAR(150),
    @unit_type_id        TINYINT,
    @parent_id           INT = NULL,
    @boundary_wkt        NVARCHAR(MAX) = NULL,
    @is_active           BIT = NULL,
    @effective_from      DATE = NULL,
    @effective_to        DATE = NULL,
    @merged_into_unit_id INT = NULL,
    @created_at          DATETIME2(0) = NULL,
    @updated_at          DATETIME2(0) = NULL,
    @new_id              INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_admin_units (unit_code, name, unit_type_id, parent_id, boundary, is_active,
                                 effective_from, effective_to, merged_into_unit_id, created_at, updated_at)
    VALUES (@unit_code, @name, @unit_type_id, @parent_id,
            geography::STGeomFromText(@boundary_wkt, 4326), ISNULL(@is_active, 1),
            @effective_from, @effective_to, @merged_into_unit_id,
            ISNULL(@created_at, SYSUTCDATETIME()), @updated_at);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_units_Update
    @id                  INT,
    @unit_code           VARCHAR(30),
    @name                NVARCHAR(150),
    @unit_type_id        TINYINT,
    @parent_id           INT = NULL,
    @boundary_wkt        NVARCHAR(MAX) = NULL,
    @is_active           BIT,
    @effective_from      DATE = NULL,
    @effective_to        DATE = NULL,
    @merged_into_unit_id INT = NULL,
    @updated_at          DATETIME2(0) = NULL
AS
BEGIN
    UPDATE tbl_admin_units
    SET unit_code           = @unit_code,
        name                = @name,
        unit_type_id        = @unit_type_id,
        parent_id           = @parent_id,
        boundary            = geography::STGeomFromText(@boundary_wkt, 4326),
        is_active           = @is_active,
        effective_from      = @effective_from,
        effective_to        = @effective_to,
        merged_into_unit_id = @merged_into_unit_id,
        updated_at          = @updated_at
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_units_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_admin_units WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_units_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, unit_code, name, unit_type_id, parent_id, unit_path,
           boundary.STAsText() AS boundary_wkt, is_active, effective_from, effective_to,
           merged_into_unit_id, created_at, updated_at
    FROM tbl_admin_units;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_admin_units_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, unit_code, name, unit_type_id, parent_id, unit_path,
           boundary.STAsText() AS boundary_wkt, is_active, effective_from, effective_to,
           merged_into_unit_id, created_at, updated_at
    FROM tbl_admin_units
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   D. CRUD: tbl_roles
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_roles_Insert
    @role_code   VARCHAR(30),
    @role_name   NVARCHAR(100),
    @description NVARCHAR(300) = NULL,
    @is_system   BIT = NULL,
    @is_active   BIT = NULL,
    @created_at  DATETIME2(0) = NULL,
    @new_id      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_roles (role_code, role_name, description, is_system, is_active, created_at)
    VALUES (@role_code, @role_name, @description, ISNULL(@is_system, 0), ISNULL(@is_active, 1),
            ISNULL(@created_at, SYSUTCDATETIME()));
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_roles_Update
    @id          INT,
    @role_code   VARCHAR(30),
    @role_name   NVARCHAR(100),
    @description NVARCHAR(300) = NULL,
    @is_system   BIT,
    @is_active   BIT
AS
BEGIN
    UPDATE tbl_roles
    SET role_code = @role_code, role_name = @role_name, description = @description,
        is_system = @is_system, is_active = @is_active
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_roles_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_roles WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_roles_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, role_code, role_name, description, is_system, is_active, created_at FROM tbl_roles;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_roles_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, role_code, role_name, description, is_system, is_active, created_at
    FROM tbl_roles WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   E. CRUD: tbl_permissions
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_permissions_Insert
    @permission_code VARCHAR(60),
    @permission_name NVARCHAR(150),
    @module          VARCHAR(30),
    @new_id          INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_permissions (permission_code, permission_name, module)
    VALUES (@permission_code, @permission_name, @module);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_permissions_Update
    @id              INT,
    @permission_code VARCHAR(60),
    @permission_name NVARCHAR(150),
    @module          VARCHAR(30)
AS
BEGIN
    UPDATE tbl_permissions
    SET permission_code = @permission_code, permission_name = @permission_name, module = @module
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_permissions_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_permissions WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_permissions_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, permission_code, permission_name, module FROM tbl_permissions;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_permissions_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, permission_code, permission_name, module FROM tbl_permissions WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   F. Khoa kep: tbl_role_permissions (muc 2 va 3.2 cua hop dong)
   Insert va Delete khong OUTPUT; Delete tra so dong (khong NOCOUNT).
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_role_permissions_Insert
    @role_id       INT,
    @permission_id INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_role_permissions (role_id, permission_id)
    VALUES (@role_id, @permission_id);
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_role_permissions_Delete
    @role_id       INT,
    @permission_id INT
AS
BEGIN
    DELETE FROM tbl_role_permissions
    WHERE role_id = @role_id AND permission_id = @permission_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_role_permissions_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT role_id, permission_id FROM tbl_role_permissions;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_role_permissions_ReadByRole
    @role_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.permission_code
    FROM tbl_role_permissions rp
    JOIN tbl_permissions p ON p.id = rp.permission_id
    WHERE rp.role_id = @role_id
    ORDER BY p.permission_code;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_role_permissions_DeleteByRole
    @role_id INT
AS
BEGIN
    DELETE FROM tbl_role_permissions WHERE role_id = @role_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   G. CRUD: tbl_users (created_at khong sua; security_stamp mac dinh NEWID())
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_users_Insert
    @username           VARCHAR(50) = NULL,
    @password_hash      VARCHAR(255) = NULL,
    @full_name          NVARCHAR(150),
    @phone              VARCHAR(15) = NULL,
    @email              VARCHAR(150) = NULL,
    @is_active          BIT = NULL,
    @security_stamp     UNIQUEIDENTIFIER = NULL,
    @failed_login_count TINYINT = NULL,
    @locked_until       DATETIME2(0) = NULL,
    @last_login_at      DATETIME2(0) = NULL,
    @created_at         DATETIME2(0) = NULL,
    @updated_at         DATETIME2(0) = NULL,
    @new_id             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_users (username, password_hash, full_name, phone, email, is_active, security_stamp,
                           failed_login_count, locked_until, last_login_at, created_at, updated_at)
    VALUES (@username, @password_hash, @full_name, @phone, @email, ISNULL(@is_active, 1),
            ISNULL(@security_stamp, NEWID()), ISNULL(@failed_login_count, 0), @locked_until, @last_login_at,
            ISNULL(@created_at, SYSUTCDATETIME()), @updated_at);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_users_Update
    @id                 INT,
    @username           VARCHAR(50) = NULL,
    @password_hash      VARCHAR(255) = NULL,
    @full_name          NVARCHAR(150),
    @phone              VARCHAR(15) = NULL,
    @email              VARCHAR(150) = NULL,
    @is_active          BIT,
    @security_stamp     UNIQUEIDENTIFIER,
    @failed_login_count TINYINT,
    @locked_until       DATETIME2(0) = NULL,
    @last_login_at      DATETIME2(0) = NULL,
    @updated_at         DATETIME2(0) = NULL
AS
BEGIN
    UPDATE tbl_users
    SET username = @username, password_hash = @password_hash, full_name = @full_name,
        phone = @phone, email = @email, is_active = @is_active, security_stamp = @security_stamp,
        failed_login_count = @failed_login_count, locked_until = @locked_until,
        last_login_at = @last_login_at, updated_at = @updated_at
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_users_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_users WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_users_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, username, password_hash, full_name, phone, email, is_active, security_stamp,
           failed_login_count, locked_until, last_login_at, created_at, updated_at
    FROM tbl_users;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_users_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, username, password_hash, full_name, phone, email, is_active, security_stamp,
           failed_login_count, locked_until, last_login_at, created_at, updated_at
    FROM tbl_users WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   H. CRUD: tbl_user_roles (assigned_at la thoi diem gan, khong sua)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_user_roles_Insert
    @user_id             INT,
    @role_id             INT,
    @admin_unit_id       INT = NULL,
    @assigned_by_user_id INT = NULL,
    @assigned_at         DATETIME2(0) = NULL,
    @expires_at          DATETIME2(0) = NULL,
    @is_active           BIT = NULL,
    @new_id              INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_user_roles (user_id, role_id, admin_unit_id, assigned_by_user_id, assigned_at, expires_at, is_active)
    VALUES (@user_id, @role_id, @admin_unit_id, @assigned_by_user_id,
            ISNULL(@assigned_at, SYSUTCDATETIME()), @expires_at, ISNULL(@is_active, 1));
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_user_roles_Update
    @id                  INT,
    @user_id             INT,
    @role_id             INT,
    @admin_unit_id       INT = NULL,
    @assigned_by_user_id INT = NULL,
    @expires_at          DATETIME2(0) = NULL,
    @is_active           BIT
AS
BEGIN
    UPDATE tbl_user_roles
    SET user_id = @user_id, role_id = @role_id, admin_unit_id = @admin_unit_id,
        assigned_by_user_id = @assigned_by_user_id, expires_at = @expires_at, is_active = @is_active
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_user_roles_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_user_roles WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_user_roles_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, user_id, role_id, admin_unit_id, assigned_by_user_id, assigned_at, expires_at, is_active
    FROM tbl_user_roles;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_user_roles_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, user_id, role_id, admin_unit_id, assigned_by_user_id, assigned_at, expires_at, is_active
    FROM tbl_user_roles WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   I. CRUD: tbl_refresh_tokens (BIGINT)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_refresh_tokens_Insert
    @user_id              INT,
    @token_hash           CHAR(64),
    @expires_at           DATETIME2(0),
    @created_at           DATETIME2(0) = NULL,
    @created_by_ip        VARCHAR(45) = NULL,
    @revoked_at           DATETIME2(0) = NULL,
    @replaced_by_token_id BIGINT = NULL,
    @security_stamp       UNIQUEIDENTIFIER,
    @new_id               BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_refresh_tokens (user_id, token_hash, expires_at, created_at, created_by_ip,
                                    revoked_at, replaced_by_token_id, security_stamp)
    VALUES (@user_id, @token_hash, @expires_at, ISNULL(@created_at, SYSUTCDATETIME()), @created_by_ip,
            @revoked_at, @replaced_by_token_id, @security_stamp);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_refresh_tokens_Update
    @id                   BIGINT,
    @user_id              INT,
    @token_hash           CHAR(64),
    @expires_at           DATETIME2(0),
    @created_by_ip        VARCHAR(45) = NULL,
    @revoked_at           DATETIME2(0) = NULL,
    @replaced_by_token_id BIGINT = NULL,
    @security_stamp       UNIQUEIDENTIFIER
AS
BEGIN
    UPDATE tbl_refresh_tokens
    SET user_id = @user_id, token_hash = @token_hash, expires_at = @expires_at,
        created_by_ip = @created_by_ip, revoked_at = @revoked_at,
        replaced_by_token_id = @replaced_by_token_id, security_stamp = @security_stamp
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_refresh_tokens_Delete
    @id BIGINT
AS
BEGIN
    DELETE FROM tbl_refresh_tokens WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_refresh_tokens_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, user_id, token_hash, expires_at, created_at, created_by_ip, revoked_at,
           replaced_by_token_id, security_stamp
    FROM tbl_refresh_tokens;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_refresh_tokens_ReadById
    @id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, user_id, token_hash, expires_at, created_at, created_by_ip, revoked_at,
           replaced_by_token_id, security_stamp
    FROM tbl_refresh_tokens WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   J. CRUD: tbl_categories
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_categories_Insert
    @category_code    VARCHAR(30),
    @category_name    NVARCHAR(100),
    @default_priority TINYINT,
    @is_active        BIT = NULL,
    @new_id           INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_categories (category_code, category_name, default_priority, is_active)
    VALUES (@category_code, @category_name, @default_priority, ISNULL(@is_active, 1));
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_categories_Update
    @id               INT,
    @category_code    VARCHAR(30),
    @category_name    NVARCHAR(100),
    @default_priority TINYINT,
    @is_active        BIT
AS
BEGIN
    UPDATE tbl_categories
    SET category_code = @category_code, category_name = @category_name,
        default_priority = @default_priority, is_active = @is_active
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_categories_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_categories WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_categories_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, category_code, category_name, default_priority, is_active FROM tbl_categories;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_categories_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, category_code, category_name, default_priority, is_active FROM tbl_categories WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   K. CRUD: tbl_sla_policies
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_sla_policies_Insert
    @priority           TINYINT,
    @sla_level          TINYINT,
    @sla_hours          SMALLINT,
    @escalation_percent TINYINT = NULL,
    @description        NVARCHAR(200) = NULL,
    @new_id             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_sla_policies (priority, sla_level, sla_hours, escalation_percent, description)
    VALUES (@priority, @sla_level, @sla_hours, ISNULL(@escalation_percent, 80), @description);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_sla_policies_Update
    @id                 INT,
    @priority           TINYINT,
    @sla_level          TINYINT,
    @sla_hours          SMALLINT,
    @escalation_percent TINYINT,
    @description        NVARCHAR(200) = NULL
AS
BEGIN
    UPDATE tbl_sla_policies
    SET priority = @priority, sla_level = @sla_level, sla_hours = @sla_hours,
        escalation_percent = @escalation_percent, description = @description
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_sla_policies_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_sla_policies WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_sla_policies_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, priority, sla_level, sla_hours, escalation_percent, description FROM tbl_sla_policies;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_sla_policies_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, priority, sla_level, sla_hours, escalation_percent, description
    FROM tbl_sla_policies WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   L. CRUD: tbl_grids (boundary_wkt -> geography)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_grids_Insert
    @grid_code           VARCHAR(50),
    @name                NVARCHAR(150),
    @admin_unit_id       INT,
    @boundary_wkt        NVARCHAR(MAX),
    @assigned_officer_id INT = NULL,
    @is_active           BIT = NULL,
    @created_at          DATETIME2(0) = NULL,
    @updated_at          DATETIME2(0) = NULL,
    @new_id              INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_grids (grid_code, name, admin_unit_id, boundary, assigned_officer_id, is_active, created_at, updated_at)
    VALUES (@grid_code, @name, @admin_unit_id, geography::STGeomFromText(@boundary_wkt, 4326),
            @assigned_officer_id, ISNULL(@is_active, 1), ISNULL(@created_at, SYSUTCDATETIME()), @updated_at);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grids_Update
    @id                  INT,
    @grid_code           VARCHAR(50),
    @name                NVARCHAR(150),
    @admin_unit_id       INT,
    @boundary_wkt        NVARCHAR(MAX),
    @assigned_officer_id INT = NULL,
    @is_active           BIT,
    @updated_at          DATETIME2(0) = NULL
AS
BEGIN
    UPDATE tbl_grids
    SET grid_code = @grid_code, name = @name, admin_unit_id = @admin_unit_id,
        boundary = geography::STGeomFromText(@boundary_wkt, 4326),
        assigned_officer_id = @assigned_officer_id, is_active = @is_active, updated_at = @updated_at
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grids_Delete
    @id INT
AS
BEGIN
    DELETE FROM tbl_grids WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grids_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, grid_code, name, admin_unit_id, boundary.STAsText() AS boundary_wkt,
           assigned_officer_id, is_active, created_at, updated_at
    FROM tbl_grids;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grids_ReadById
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, grid_code, name, admin_unit_id, boundary.STAsText() AS boundary_wkt,
           assigned_officer_id, is_active, created_at, updated_at
    FROM tbl_grids WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   M. CRUD: tbl_grid_officers (khoa kep grid_id + user_id)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_grid_officers_Insert
    @grid_id     INT,
    @user_id     INT,
    @is_primary  BIT = NULL,
    @assigned_at DATETIME2(0) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_grid_officers (grid_id, user_id, is_primary, assigned_at)
    VALUES (@grid_id, @user_id, ISNULL(@is_primary, 0), ISNULL(@assigned_at, SYSUTCDATETIME()));
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grid_officers_Delete
    @grid_id INT,
    @user_id INT
AS
BEGIN
    DELETE FROM tbl_grid_officers WHERE grid_id = @grid_id AND user_id = @user_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grid_officers_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT grid_id, user_id, is_primary, assigned_at FROM tbl_grid_officers;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grid_officers_ReadByGrid
    @grid_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT go.user_id, u.full_name, u.phone, go.is_primary
    FROM tbl_grid_officers go
    JOIN tbl_users u ON u.id = go.user_id
    WHERE go.grid_id = @grid_id
    ORDER BY go.is_primary DESC, u.full_name;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_grid_officers_DeleteByGrid
    @grid_id INT
AS
BEGIN
    DELETE FROM tbl_grid_officers WHERE grid_id = @grid_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   N. CRUD: tbl_incidents (BIGINT, location_point -> geography)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_Insert
    @tracking_code       VARCHAR(20),
    @reporter_user_id    INT = NULL,
    @reporter_phone      VARCHAR(15) = NULL,
    @description         NVARCHAR(1000) = NULL,
    @category_id         INT = NULL,
    @ai_label            NVARCHAR(100) = NULL,
    @ai_confidence       DECIMAL(5,4) = NULL,
    @is_manual_review    BIT = NULL,
    @location_point_wkt  NVARCHAR(MAX),
    @address_text        NVARCHAR(255) = NULL,
    @grid_id             INT = NULL,
    @admin_unit_id       INT = NULL,
    @assigned_officer_id INT = NULL,
    @parent_incident_id  BIGINT = NULL,
    @duplicate_count     INT = NULL,
    @status              TINYINT = NULL,
    @priority            TINYINT = NULL,
    @sla_policy_id       INT = NULL,
    @sla_deadline        DATETIME2(0) = NULL,
    @accepted_at         DATETIME2(0) = NULL,
    @is_overdue          BIT = NULL,
    @escalated_at        DATETIME2(0) = NULL,
    @resolved_at         DATETIME2(0) = NULL,
    @created_at          DATETIME2(0) = NULL,
    @updated_at          DATETIME2(0) = NULL,
    @processed_at        DATETIME2(0) = NULL,
    @new_id              BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_incidents (tracking_code, reporter_user_id, reporter_phone, description, category_id,
        ai_label, ai_confidence, is_manual_review, location_point, address_text, grid_id, admin_unit_id,
        assigned_officer_id, parent_incident_id, duplicate_count, status, priority, sla_policy_id, sla_deadline,
        accepted_at, is_overdue, escalated_at, resolved_at, created_at, updated_at, processed_at)
    VALUES (@tracking_code, @reporter_user_id, @reporter_phone, @description, @category_id,
        @ai_label, @ai_confidence, ISNULL(@is_manual_review, 0), geography::STGeomFromText(@location_point_wkt, 4326),
        @address_text, @grid_id, @admin_unit_id,
        @assigned_officer_id, @parent_incident_id, ISNULL(@duplicate_count, 0), ISNULL(@status, 0), ISNULL(@priority, 2),
        @sla_policy_id, @sla_deadline, @accepted_at, ISNULL(@is_overdue, 0), @escalated_at, @resolved_at,
        ISNULL(@created_at, SYSUTCDATETIME()), @updated_at, @processed_at);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incidents_Update
    @id                  BIGINT,
    @tracking_code       VARCHAR(20),
    @reporter_user_id    INT = NULL,
    @reporter_phone      VARCHAR(15) = NULL,
    @description         NVARCHAR(1000) = NULL,
    @category_id         INT = NULL,
    @ai_label            NVARCHAR(100) = NULL,
    @ai_confidence       DECIMAL(5,4) = NULL,
    @is_manual_review    BIT,
    @location_point_wkt  NVARCHAR(MAX),
    @address_text        NVARCHAR(255) = NULL,
    @grid_id             INT = NULL,
    @admin_unit_id       INT = NULL,
    @assigned_officer_id INT = NULL,
    @parent_incident_id  BIGINT = NULL,
    @duplicate_count     INT,
    @status              TINYINT,
    @priority            TINYINT,
    @sla_policy_id       INT = NULL,
    @sla_deadline        DATETIME2(0) = NULL,
    @accepted_at         DATETIME2(0) = NULL,
    @is_overdue          BIT,
    @escalated_at        DATETIME2(0) = NULL,
    @resolved_at         DATETIME2(0) = NULL,
    @updated_at          DATETIME2(0) = NULL,
    @processed_at        DATETIME2(0) = NULL
AS
BEGIN
    UPDATE tbl_incidents
    SET tracking_code = @tracking_code, reporter_user_id = @reporter_user_id, reporter_phone = @reporter_phone,
        description = @description, category_id = @category_id, ai_label = @ai_label,
        ai_confidence = @ai_confidence, is_manual_review = @is_manual_review,
        location_point = geography::STGeomFromText(@location_point_wkt, 4326),
        address_text = @address_text, grid_id = @grid_id, admin_unit_id = @admin_unit_id,
        assigned_officer_id = @assigned_officer_id, parent_incident_id = @parent_incident_id,
        duplicate_count = @duplicate_count, status = @status, priority = @priority,
        sla_policy_id = @sla_policy_id, sla_deadline = @sla_deadline, accepted_at = @accepted_at,
        is_overdue = @is_overdue, escalated_at = @escalated_at, resolved_at = @resolved_at,
        updated_at = @updated_at, processed_at = @processed_at
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incidents_Delete
    @id BIGINT
AS
BEGIN
    DELETE FROM tbl_incidents WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incidents_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.id, i.tracking_code, i.reporter_user_id, i.reporter_phone, i.description, i.category_id,
           i.ai_label, i.ai_confidence, i.is_manual_review, i.location_point.STAsText() AS location_point_wkt,
           i.address_text, i.grid_id, i.admin_unit_id, i.assigned_officer_id, i.parent_incident_id,
           i.duplicate_count, i.status, i.priority, i.sla_policy_id, i.sla_deadline, i.accepted_at,
           i.is_overdue, i.escalated_at, i.resolved_at, i.created_at, i.updated_at, i.processed_at
    FROM tbl_incidents i;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incidents_ReadById
    @id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.id, i.tracking_code, i.reporter_user_id, i.reporter_phone, i.description, i.category_id,
           i.ai_label, i.ai_confidence, i.is_manual_review, i.location_point.STAsText() AS location_point_wkt,
           i.address_text, i.grid_id, i.admin_unit_id, i.assigned_officer_id, i.parent_incident_id,
           i.duplicate_count, i.status, i.priority, i.sla_policy_id, i.sla_deadline, i.accepted_at,
           i.is_overdue, i.escalated_at, i.resolved_at, i.created_at, i.updated_at, i.processed_at
    FROM tbl_incidents i
    WHERE i.id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   O. CRUD: tbl_incident_images (BIGINT, capture_point -> geography, NULL duoc)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_incident_images_Insert
    @incident_id        BIGINT,
    @image_type         VARCHAR(10),
    @image_url          NVARCHAR(500),
    @capture_point_wkt  NVARCHAR(MAX) = NULL,
    @uploaded_by_user_id INT = NULL,
    @uploaded_at        DATETIME2(0) = NULL,
    @new_id             BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_incident_images (incident_id, image_type, image_url, capture_point, uploaded_by_user_id, uploaded_at)
    VALUES (@incident_id, @image_type, @image_url, geography::STGeomFromText(@capture_point_wkt, 4326),
            @uploaded_by_user_id, ISNULL(@uploaded_at, SYSUTCDATETIME()));
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_images_Update
    @id                  BIGINT,
    @incident_id         BIGINT,
    @image_type          VARCHAR(10),
    @image_url           NVARCHAR(500),
    @capture_point_wkt   NVARCHAR(MAX) = NULL,
    @uploaded_by_user_id INT = NULL
AS
BEGIN
    UPDATE tbl_incident_images
    SET incident_id = @incident_id, image_type = @image_type, image_url = @image_url,
        capture_point = geography::STGeomFromText(@capture_point_wkt, 4326),
        uploaded_by_user_id = @uploaded_by_user_id
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_images_Delete
    @id BIGINT
AS
BEGIN
    DELETE FROM tbl_incident_images WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_images_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, image_type, image_url, capture_point.STAsText() AS capture_point_wkt,
           uploaded_by_user_id, uploaded_at
    FROM tbl_incident_images;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_images_ReadById
    @id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, image_type, image_url, capture_point.STAsText() AS capture_point_wkt,
           uploaded_by_user_id, uploaded_at
    FROM tbl_incident_images WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   P. CRUD: tbl_incident_logs (BIGINT)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_incident_logs_Insert
    @incident_id       BIGINT,
    @action_by_user_id INT = NULL,
    @action_type       VARCHAR(30),
    @action_note       NVARCHAR(500),
    @old_status        TINYINT = NULL,
    @new_status        TINYINT = NULL,
    @created_at        DATETIME2(0) = NULL,
    @new_id            BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_incident_logs (incident_id, action_by_user_id, action_type, action_note, old_status, new_status, created_at)
    VALUES (@incident_id, @action_by_user_id, @action_type, @action_note, @old_status, @new_status,
            ISNULL(@created_at, SYSUTCDATETIME()));
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_logs_Update
    @id                BIGINT,
    @incident_id       BIGINT,
    @action_by_user_id INT = NULL,
    @action_type       VARCHAR(30),
    @action_note       NVARCHAR(500),
    @old_status        TINYINT = NULL,
    @new_status        TINYINT = NULL
AS
BEGIN
    UPDATE tbl_incident_logs
    SET incident_id = @incident_id, action_by_user_id = @action_by_user_id, action_type = @action_type,
        action_note = @action_note, old_status = @old_status, new_status = @new_status
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_logs_Delete
    @id BIGINT
AS
BEGIN
    DELETE FROM tbl_incident_logs WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_logs_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, action_by_user_id, action_type, action_note, old_status, new_status, created_at
    FROM tbl_incident_logs;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_logs_ReadById
    @id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, action_by_user_id, action_type, action_note, old_status, new_status, created_at
    FROM tbl_incident_logs WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   Q. CRUD: tbl_incident_ratings (BIGINT)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_incident_ratings_Insert
    @incident_id BIGINT,
    @rating      TINYINT,
    @feedback    NVARCHAR(500) = NULL,
    @created_at  DATETIME2(0) = NULL,
    @new_id      BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_incident_ratings (incident_id, rating, feedback, created_at)
    VALUES (@incident_id, @rating, @feedback, ISNULL(@created_at, SYSUTCDATETIME()));
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_ratings_Update
    @id          BIGINT,
    @incident_id BIGINT,
    @rating      TINYINT,
    @feedback    NVARCHAR(500) = NULL
AS
BEGIN
    UPDATE tbl_incident_ratings
    SET incident_id = @incident_id, rating = @rating, feedback = @feedback
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_ratings_Delete
    @id BIGINT
AS
BEGIN
    DELETE FROM tbl_incident_ratings WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_ratings_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, rating, feedback, created_at FROM tbl_incident_ratings;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_incident_ratings_ReadById
    @id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, rating, feedback, created_at FROM tbl_incident_ratings WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   R. CRUD: tbl_notifications (BIGINT)
   ===================================================================== */
CREATE OR ALTER PROCEDURE SP_tbl_notifications_Insert
    @user_id           INT,
    @incident_id       BIGINT = NULL,
    @notification_type VARCHAR(30),
    @title             NVARCHAR(200),
    @content           NVARCHAR(500) = NULL,
    @is_read           BIT = NULL,
    @created_at        DATETIME2(0) = NULL,
    @read_at           DATETIME2(0) = NULL,
    @new_id            BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO tbl_notifications (user_id, incident_id, notification_type, title, content, is_read, created_at, read_at)
    VALUES (@user_id, @incident_id, @notification_type, @title, @content, ISNULL(@is_read, 0),
            ISNULL(@created_at, SYSUTCDATETIME()), @read_at);
    SET @new_id = SCOPE_IDENTITY();
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_notifications_Update
    @id                BIGINT,
    @user_id           INT,
    @incident_id       BIGINT = NULL,
    @notification_type VARCHAR(30),
    @title             NVARCHAR(200),
    @content           NVARCHAR(500) = NULL,
    @is_read           BIT,
    @read_at           DATETIME2(0) = NULL
AS
BEGIN
    UPDATE tbl_notifications
    SET user_id = @user_id, incident_id = @incident_id, notification_type = @notification_type,
        title = @title, content = @content, is_read = @is_read, read_at = @read_at
    WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_notifications_Delete
    @id BIGINT
AS
BEGIN
    DELETE FROM tbl_notifications WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_notifications_ReadAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, user_id, incident_id, notification_type, title, content, is_read, created_at, read_at
    FROM tbl_notifications;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

CREATE OR ALTER PROCEDURE SP_tbl_notifications_ReadById
    @id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, user_id, incident_id, notification_type, title, content, is_read, created_at, read_at
    FROM tbl_notifications WHERE id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   S. Xac thuc va phan quyen (muc 4.1)
   ===================================================================== */

/* 1. Phạm vi và quyền hiệu lực của người dùng, nạp khi đăng nhập */
CREATE OR ALTER PROCEDURE SP_Auth_LoadScopes
    @user_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT
           CAST(r.role_code AS NVARCHAR(30)) AS role_code,
           ur.admin_unit_id,
           au.unit_path,
           au.name AS unit_name,
           p.permission_code
    FROM tbl_users u
    JOIN tbl_user_roles ur       ON ur.user_id = u.id
                                AND ur.is_active = 1
                                AND (ur.expires_at IS NULL OR ur.expires_at > SYSUTCDATETIME())
    JOIN tbl_roles r             ON r.id = ur.role_id AND r.is_active = 1
    JOIN tbl_role_permissions rp ON rp.role_id = r.id
    JOIN tbl_permissions p       ON p.id = rp.permission_id
    LEFT JOIN tbl_admin_units au ON au.id = ur.admin_unit_id
    WHERE u.id = @user_id AND u.is_active = 1;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 9. Người có quyền @permission_code, kèm phạm vi (NULL = toàn hệ thống) */
CREATE OR ALTER PROCEDURE SP_Auth_FindUsersWithPermission
    @permission_code VARCHAR(60),
    @unit_path       VARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT ur.user_id, au.unit_path AS scope_path
    FROM tbl_user_roles ur
    JOIN tbl_users u             ON u.id = ur.user_id AND u.is_active = 1
    JOIN tbl_roles r             ON r.id = ur.role_id AND r.is_active = 1
    JOIN tbl_role_permissions rp ON rp.role_id = r.id
    JOIN tbl_permissions p       ON p.id = rp.permission_id AND p.permission_code = @permission_code
    LEFT JOIN tbl_admin_units au ON au.id = ur.admin_unit_id
    WHERE ur.is_active = 1
      AND (ur.expires_at IS NULL OR ur.expires_at > SYSUTCDATETIME())
      AND (@unit_path IS NULL OR ur.admin_unit_id IS NULL OR @unit_path LIKE au.unit_path + '%');
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 10. Cán bộ ô lưới phụ trách một đơn vị (hoặc mọi cán bộ khi @unit_path NULL) */
CREATE OR ALTER PROCEDURE SP_Auth_FindOfficersForUnit
    @unit_path VARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT u.id AS user_id, u.full_name, u.phone, au.unit_path AS scope_path
    FROM tbl_user_roles ur
    JOIN tbl_users u      ON u.id = ur.user_id AND u.is_active = 1
    JOIN tbl_roles r      ON r.id = ur.role_id AND r.role_code = 'GRID_OFFICER' AND r.is_active = 1
    LEFT JOIN tbl_admin_units au ON au.id = ur.admin_unit_id
    WHERE ur.is_active = 1
      AND (ur.expires_at IS NULL OR ur.expires_at > SYSUTCDATETIME())
      AND (@unit_path IS NULL OR ur.admin_unit_id IS NULL OR @unit_path LIKE au.unit_path + '%');
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 2. Người dùng theo tên đăng nhập (kể cả password_hash) */
CREATE OR ALTER PROCEDURE SP_tbl_users_ReadByUsername
    @username VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, username, password_hash, full_name, phone, email, is_active, security_stamp,
           failed_login_count, locked_until, last_login_at, created_at, updated_at
    FROM tbl_users
    WHERE username = @username;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 2b. Thong bao cua mot su co (dung khi xoa phan anh) */
CREATE OR ALTER PROCEDURE SP_tbl_notifications_ReadByIncident
    @incident_id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM tbl_notifications WHERE incident_id = @incident_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 3. Danh sách người dùng có phân trang (page_index đánh số từ 1) */
CREATE OR ALTER PROCEDURE SP_tbl_users_List
    @page_index  INT,
    @page_size   INT,
    @search      NVARCHAR(200) = NULL,
    @role_code   VARCHAR(30) = NULL,
    @unit_path   VARCHAR(400) = NULL,
    @scope_paths NVARCHAR(MAX) = NULL,
    @locked      BIT = NULL          -- 1: chi nguoi dang bi khoa; 0: chi nguoi khong bi khoa; NULL: tat ca
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ps   INT = CASE WHEN @page_size IS NULL OR @page_size < 1 THEN 20
                             WHEN @page_size > 200 THEN 200 ELSE @page_size END;
    DECLARE @pi   INT = CASE WHEN @page_index IS NULL OR @page_index < 1 THEN 1 ELSE @page_index END;
    DECLARE @skip INT = (@pi - 1) * @ps;
    DECLARE @ids TABLE (user_id INT NOT NULL PRIMARY KEY);

    -- Nguoi dung co vai tro dang hieu luc khop bo loc, nam trong pham vi
    INSERT INTO @ids (user_id)
    SELECT DISTINCT ur.user_id
    FROM tbl_user_roles ur
    JOIN tbl_roles r      ON r.id = ur.role_id AND r.is_active = 1
    JOIN tbl_users u      ON u.id = ur.user_id
    LEFT JOIN tbl_admin_units au ON au.id = ur.admin_unit_id
    WHERE ur.is_active = 1
      AND (ur.expires_at IS NULL OR ur.expires_at > SYSUTCDATETIME())
      AND (@locked IS NULL
           OR (@locked = 1 AND u.locked_until > SYSUTCDATETIME())
           OR (@locked = 0 AND (u.locked_until IS NULL OR u.locked_until <= SYSUTCDATETIME())))
      AND (@role_code IS NULL OR r.role_code = @role_code)
      AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
      AND (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                           WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
      AND (@search IS NULL
           OR u.full_name LIKE N'%' + @search + N'%'
           OR u.username  LIKE N'%' + @search + N'%'
           OR u.phone     LIKE N'%' + @search + N'%'
           OR u.email     LIKE N'%' + @search + N'%');

    SELECT u.id, u.username, u.full_name, u.phone, u.email, u.is_active, u.locked_until, u.last_login_at,
           (SELECT STRING_AGG(r.role_code + N'@' + ISNULL(au.name, N'Toàn hệ thống'), N', ')
                   WITHIN GROUP (ORDER BY r.role_code, au.name)
            FROM tbl_user_roles ur
            JOIN tbl_roles r ON r.id = ur.role_id
            LEFT JOIN tbl_admin_units au ON au.id = ur.admin_unit_id
            WHERE ur.user_id = u.id
              AND ur.is_active = 1
              AND r.is_active = 1
              AND (ur.expires_at IS NULL OR ur.expires_at > SYSUTCDATETIME())
              AND (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                                   WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
           ) AS role_summary
    FROM @ids x
    JOIN tbl_users u ON u.id = x.user_id
    ORDER BY u.full_name, u.id
    OFFSET @skip ROWS FETCH NEXT @ps ROWS ONLY;

    SELECT COUNT(*) AS total_count FROM @ids;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 4. Vai trò của một người dùng */
CREATE OR ALTER PROCEDURE SP_tbl_user_roles_ReadByUser
    @user_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ur.id, ur.user_id, ur.role_id, r.role_code, r.role_name,
           ur.admin_unit_id, au.name AS unit_name, au.unit_path,
           ur.assigned_by_user_id, ub.full_name AS assigned_by_name,
           ur.assigned_at, ur.expires_at, ur.is_active
    FROM tbl_user_roles ur
    JOIN tbl_roles r             ON r.id = ur.role_id
    LEFT JOIN tbl_admin_units au ON au.id = ur.admin_unit_id
    LEFT JOIN tbl_users ub       ON ub.id = ur.assigned_by_user_id
    WHERE ur.user_id = @user_id
    ORDER BY ur.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 5. Vai trò gán tại các đơn vị (danh sách id dạng '1,2,3') */
CREATE OR ALTER PROCEDURE SP_tbl_user_roles_ReadByUnits
    @unit_ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ur.id, ur.user_id, ur.role_id, r.role_code, r.role_name,
           ur.admin_unit_id, au.name AS unit_name, au.unit_path,
           ur.assigned_by_user_id, ub.full_name AS assigned_by_name,
           ur.assigned_at, ur.expires_at, ur.is_active
    FROM tbl_user_roles ur
    JOIN tbl_roles r             ON r.id = ur.role_id
    LEFT JOIN tbl_admin_units au ON au.id = ur.admin_unit_id
    LEFT JOIN tbl_users ub       ON ub.id = ur.assigned_by_user_id
    WHERE EXISTS (SELECT 1 FROM STRING_SPLIT(@unit_ids, ',') s
                  WHERE TRY_CAST(s.value AS INT) = ur.admin_unit_id)
    ORDER BY ur.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 6. Vai trò theo mã */
CREATE OR ALTER PROCEDURE SP_tbl_roles_ReadByCode
    @role_code VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, role_code, role_name, description, is_system, is_active, created_at
    FROM tbl_roles
    WHERE role_code = @role_code;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   T. Don vi hanh chinh, o luoi, loai don vi (muc 4.2)
   ===================================================================== */

/* 13. Cay don vi cho man hinh quan tri */
CREATE OR ALTER PROCEDURE SP_tbl_admin_units_ReadTree
    @include_inactive BIT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.id, u.unit_code, u.name, u.unit_type_id, t.type_code, t.type_name,
           t.level_id, l.level_code, l.level_order, u.parent_id, u.unit_path, u.is_active
    FROM tbl_admin_units u
    JOIN tbl_admin_unit_types t ON t.id = u.unit_type_id
    JOIN tbl_admin_levels l     ON l.id = t.level_id
    WHERE @include_inactive = 1 OR u.is_active = 1
    ORDER BY l.level_order, u.name;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 14. Chi tiết một đơn vị */
CREATE OR ALTER PROCEDURE SP_tbl_admin_units_ReadDetail
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.id, u.unit_code, u.name, u.unit_type_id, u.parent_id, u.unit_path,
           u.boundary.STAsText() AS boundary_wkt, u.is_active, u.effective_from, u.effective_to,
           u.merged_into_unit_id, u.created_at, u.updated_at,
           t.type_code, t.type_name, l.level_code,
           (SELECT COUNT(*) FROM tbl_admin_units c WHERE c.parent_id = u.id) AS child_count,
           -- So o luoi dang hoat dong gan truc tiep vao don vi (khong cong cac don vi con), khong dem o da ngung su dung
           (SELECT COUNT(*) FROM tbl_grids g WHERE g.admin_unit_id = u.id AND g.is_active = 1) AS grid_count
    FROM tbl_admin_units u
    JOIN tbl_admin_unit_types t ON t.id = u.unit_type_id
    JOIN tbl_admin_levels l     ON l.id = t.level_id
    WHERE u.id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 15. Tổ tiên của đơn vị (kể cả chính nó), từ cấp cao xuống thấp */
CREATE OR ALTER PROCEDURE SP_vw_admin_unit_ancestors_ReadByUnit
    @unit_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ancestor_id, ancestor_code, ancestor_name, ancestor_level_code, ancestor_level_order
    FROM vw_admin_unit_ancestors
    WHERE unit_id = @unit_id
    ORDER BY ancestor_level_order;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 16. Loại đơn vị kèm cấp */
CREATE OR ALTER PROCEDURE SP_tbl_admin_unit_types_ReadAllWithLevel
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.id, t.type_code, t.type_name, t.level_id, l.level_code
    FROM tbl_admin_unit_types t
    JOIN tbl_admin_levels l ON l.id = t.level_id
    ORDER BY l.level_order, t.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 17. Đơn vị theo mã */
CREATE OR ALTER PROCEDURE SP_tbl_admin_units_ReadByCode
    @unit_code VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, unit_code, name, unit_type_id, parent_id, unit_path,
           boundary.STAsText() AS boundary_wkt, is_active, effective_from, effective_to,
           merged_into_unit_id, created_at, updated_at
    FROM tbl_admin_units
    WHERE unit_code = @unit_code;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 18. Đơn vị con trực tiếp của các đơn vị cha (danh sách '1,2') */
CREATE OR ALTER PROCEDURE SP_tbl_admin_units_ReadChildrenOf
    @parent_ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.id, u.unit_code, u.name, u.unit_type_id, u.parent_id, u.unit_path,
           u.boundary.STAsText() AS boundary_wkt, u.is_active, u.effective_from, u.effective_to,
           u.merged_into_unit_id, u.created_at, u.updated_at
    FROM tbl_admin_units u
    WHERE EXISTS (SELECT 1 FROM STRING_SPLIT(@parent_ids, ',') s
                  WHERE TRY_CAST(s.value AS INT) = u.parent_id)
    ORDER BY u.name;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 19. Chi tiết ô lưới */
CREATE OR ALTER PROCEDURE SP_tbl_grids_ReadDetail
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT g.id, g.grid_code, g.name, g.admin_unit_id, g.boundary.STAsText() AS boundary_wkt,
           g.assigned_officer_id, g.is_active, g.created_at, g.updated_at,
           au.unit_path, au.name AS unit_name,
           (SELECT COUNT(*) FROM tbl_grid_officers go WHERE go.grid_id = g.id) AS officer_count
    FROM tbl_grids g
    JOIN tbl_admin_units au ON au.id = g.admin_unit_id
    WHERE g.id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 20. Ô lưới theo mã */
CREATE OR ALTER PROCEDURE SP_tbl_grids_ReadByCode
    @grid_code VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, grid_code, name, admin_unit_id, boundary.STAsText() AS boundary_wkt,
           assigned_officer_id, is_active, created_at, updated_at
    FROM tbl_grids
    WHERE grid_code = @grid_code;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 21. Ô lưới thuộc các đơn vị (danh sách '1,2') */
CREATE OR ALTER PROCEDURE SP_tbl_grids_ReadByUnits
    @unit_ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT g.id, g.grid_code, g.name, g.admin_unit_id, g.boundary.STAsText() AS boundary_wkt,
           g.assigned_officer_id, g.is_active, g.created_at, g.updated_at
    FROM tbl_grids g
    WHERE EXISTS (SELECT 1 FROM STRING_SPLIT(@unit_ids, ',') s
                  WHERE TRY_CAST(s.value AS INT) = g.admin_unit_id)
    ORDER BY g.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 22. Trạng thái màu các ô lưới (đọc vw_grid_status) */
CREATE OR ALTER PROCEDURE SP_tbl_grids_ReadStatus
    @scope_paths NVARCHAR(MAX) = NULL,
    @unit_path   VARCHAR(400) = NULL,
    @grid_id     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.grid_id, v.grid_code, v.name, v.admin_unit_id, v.hamlet_name, v.commune_id, v.commune_name,
           au.unit_path, v.boundary.STAsText() AS boundary_wkt,
           v.open_incident_count, v.overdue_incident_count, v.status_color
    FROM vw_grid_status v
    JOIN tbl_admin_units au ON au.id = v.admin_unit_id
    WHERE (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                           WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
      AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
      AND (@grid_id IS NULL OR v.grid_id = @grid_id)
    ORDER BY v.grid_code;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   U. Su co (muc 4.3)
   ===================================================================== */

/* Cột thumbnail: ảnh BEFORE đầu tiên, NULL nếu không có */
/* Projection danh sách dùng chung cho List và ReadChildren */

/* 24. Sự cố theo mã theo dõi */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_ReadByTrackingCode
    @tracking_code VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.id, i.tracking_code, i.reporter_user_id, i.reporter_phone, i.description, i.category_id,
           i.ai_label, i.ai_confidence, i.is_manual_review, i.location_point.STAsText() AS location_point_wkt,
           i.address_text, i.grid_id, i.admin_unit_id, i.assigned_officer_id, i.parent_incident_id,
           i.duplicate_count, i.status, i.priority, i.sla_policy_id, i.sla_deadline, i.accepted_at,
           i.is_overdue, i.escalated_at, i.resolved_at, i.created_at, i.updated_at, i.processed_at
    FROM tbl_incidents i
    WHERE i.tracking_code = @tracking_code;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 25. Danh sách sự cố có phân trang và bộ lọc */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_List
    @page_index          INT,
    @page_size           INT,
    @scope_paths         NVARCHAR(MAX) = NULL,
    @status_list         VARCHAR(100) = NULL,
    @unit_path           VARCHAR(400) = NULL,
    @grid_id             INT = NULL,
    @category_id         INT = NULL,
    @priority            TINYINT = NULL,
    @is_overdue          BIT = NULL,
    @is_manual_review    BIT = NULL,
    @assigned_officer_id INT = NULL,
    @from_utc            DATETIME2 = NULL,
    @to_utc              DATETIME2 = NULL,
    @search              NVARCHAR(200) = NULL,
    @include_children    BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ps   INT = CASE WHEN @page_size IS NULL OR @page_size < 1 THEN 20
                             WHEN @page_size > 200 THEN 200 ELSE @page_size END;
    DECLARE @pi   INT = CASE WHEN @page_index IS NULL OR @page_index < 1 THEN 1 ELSE @page_index END;
    DECLARE @skip INT = (@pi - 1) * @ps;
    DECLARE @ids TABLE (id BIGINT NOT NULL PRIMARY KEY);

    INSERT INTO @ids (id)
    SELECT i.id
    FROM tbl_incidents i
    LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
    WHERE (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                           WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
      AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
      AND (@status_list IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@status_list, ',') s
                                           WHERE TRY_CAST(s.value AS TINYINT) = i.status))
      AND (@grid_id IS NULL OR i.grid_id = @grid_id)
      AND (@category_id IS NULL OR i.category_id = @category_id)
      AND (@priority IS NULL OR i.priority = @priority)
      AND (@is_overdue IS NULL OR i.is_overdue = @is_overdue)
      AND (@is_manual_review IS NULL OR i.is_manual_review = @is_manual_review)
      AND (@assigned_officer_id IS NULL OR i.assigned_officer_id = @assigned_officer_id)
      AND (@from_utc IS NULL OR i.created_at >= @from_utc)
      AND (@to_utc IS NULL OR i.created_at < @to_utc)
      AND (@search IS NULL
           OR i.description LIKE N'%' + @search + N'%'
           OR i.tracking_code LIKE N'%' + @search + N'%'
           OR i.address_text LIKE N'%' + @search + N'%')
      AND (ISNULL(@include_children, 0) = 1 OR i.parent_incident_id IS NULL);

    SELECT i.id, i.tracking_code, i.status, i.priority, i.category_id, c.category_name, i.description,
           i.address_text, i.location_point.STAsText() AS location_point_wkt, i.grid_id, g.grid_code,
           i.admin_unit_id, au.name AS unit_name, au.unit_path, i.assigned_officer_id,
           uo.full_name AS officer_name, i.sla_deadline, i.accepted_at, i.resolved_at, i.is_overdue,
           i.is_manual_review, i.ai_confidence, i.duplicate_count, i.parent_incident_id, i.created_at,
           th.image_url AS thumbnail_url
    FROM @ids x
    JOIN tbl_incidents i         ON i.id = x.id
    LEFT JOIN tbl_categories c   ON c.id = i.category_id
    LEFT JOIN tbl_grids g        ON g.id = i.grid_id
    LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
    LEFT JOIN tbl_users uo       ON uo.id = i.assigned_officer_id
    OUTER APPLY (SELECT TOP (1) img.image_url
                 FROM tbl_incident_images img
                 WHERE img.incident_id = i.id AND img.image_type = 'BEFORE'
                 ORDER BY img.id) th
    ORDER BY i.created_at DESC, i.id DESC
    OFFSET @skip ROWS FETCH NEXT @ps ROWS ONLY;

    SELECT COUNT(*) AS total_count FROM @ids;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 26. Chi tiết một sự cố */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_ReadDetail
    @id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.id, i.tracking_code, i.reporter_user_id, i.reporter_phone, i.description, i.category_id,
           i.ai_label, i.ai_confidence, i.is_manual_review, i.location_point.STAsText() AS location_point_wkt,
           i.address_text, i.grid_id, i.admin_unit_id, i.assigned_officer_id, i.parent_incident_id,
           i.duplicate_count, i.status, i.priority, i.sla_policy_id, i.sla_deadline, i.accepted_at,
           i.is_overdue, i.escalated_at, i.resolved_at, i.created_at, i.updated_at, i.processed_at,
           c.category_name, g.grid_code, g.name AS grid_name, g.boundary.STAsText() AS grid_boundary_wkt,
           au.name AS unit_name, au.unit_path, uo.full_name AS officer_name,
           p.tracking_code AS parent_tracking_code,
           (SELECT COUNT(*) FROM tbl_incidents ch WHERE ch.parent_incident_id = i.id) AS child_count,
           ur.full_name AS reporter_name
    FROM tbl_incidents i
    LEFT JOIN tbl_categories c   ON c.id = i.category_id
    LEFT JOIN tbl_grids g        ON g.id = i.grid_id
    LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
    LEFT JOIN tbl_users uo       ON uo.id = i.assigned_officer_id
    LEFT JOIN tbl_incidents p    ON p.id = i.parent_incident_id
    LEFT JOIN tbl_users ur       ON ur.id = i.reporter_user_id
    WHERE i.id = @id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 27. Sự cố con (trùng) của một sự cố gốc */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_ReadChildren
    @parent_id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.id, i.tracking_code, i.status, i.priority, i.category_id, c.category_name, i.description,
           i.address_text, i.location_point.STAsText() AS location_point_wkt, i.grid_id, g.grid_code,
           i.admin_unit_id, au.name AS unit_name, au.unit_path, i.assigned_officer_id,
           uo.full_name AS officer_name, i.sla_deadline, i.accepted_at, i.resolved_at, i.is_overdue,
           i.is_manual_review, i.ai_confidence, i.duplicate_count, i.parent_incident_id, i.created_at,
           th.image_url AS thumbnail_url
    FROM tbl_incidents i
    LEFT JOIN tbl_categories c   ON c.id = i.category_id
    LEFT JOIN tbl_grids g        ON g.id = i.grid_id
    LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
    LEFT JOIN tbl_users uo       ON uo.id = i.assigned_officer_id
    OUTER APPLY (SELECT TOP (1) img.image_url
                 FROM tbl_incident_images img
                 WHERE img.incident_id = i.id AND img.image_type = 'BEFORE'
                 ORDER BY img.id) th
    WHERE i.parent_incident_id = @parent_id
    ORDER BY i.created_at, i.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 28. Sự cố con còn đang mở */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_ReadOpenChildren
    @parent_id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.id, i.tracking_code, i.reporter_user_id, i.reporter_phone, i.description, i.category_id,
           i.ai_label, i.ai_confidence, i.is_manual_review, i.location_point.STAsText() AS location_point_wkt,
           i.address_text, i.grid_id, i.admin_unit_id, i.assigned_officer_id, i.parent_incident_id,
           i.duplicate_count, i.status, i.priority, i.sla_policy_id, i.sla_deadline, i.accepted_at,
           i.is_overdue, i.escalated_at, i.resolved_at, i.created_at, i.updated_at, i.processed_at
    FROM tbl_incidents i
    WHERE i.parent_incident_id = @parent_id
      AND i.status IN (0, 1, 2);
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 29. Sự cố chưa qua pipeline nền */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_ReadPendingPipeline
    @cutoff_utc DATETIME2,
    @take       INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@take) i.id
    FROM tbl_incidents i
    WHERE i.processed_at IS NULL
      AND i.created_at <= @cutoff_utc
    ORDER BY i.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 30. Đánh dấu leo thang (chiếm mốc bằng UPDATE ... OUTPUT, khớp SlaEngine) */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_MarkEscalated
    @now_utc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE i
    SET escalated_at = @now_utc,
        updated_at   = @now_utc
    OUTPUT inserted.id, inserted.tracking_code, inserted.grid_id, inserted.admin_unit_id, inserted.sla_deadline
    FROM tbl_incidents i
    JOIN tbl_sla_policies p ON p.id = i.sla_policy_id
    WHERE i.status IN (0, 1, 2)
      AND i.parent_incident_id IS NULL
      AND i.accepted_at IS NULL
      AND i.escalated_at IS NULL
      AND i.sla_deadline IS NOT NULL
      AND @now_utc >= DATEADD(SECOND, p.sla_hours * 36 * p.escalation_percent,
                              DATEADD(HOUR, -p.sla_hours, i.sla_deadline));
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 31. Đánh dấu quá hạn (khớp SlaEngine) */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_MarkOverdue
    @now_utc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE tbl_incidents
    SET is_overdue = 1,
        updated_at = @now_utc
    OUTPUT inserted.id, inserted.tracking_code, inserted.grid_id, inserted.admin_unit_id, inserted.sla_deadline
    WHERE status IN (0, 1, 2)
      AND parent_incident_id IS NULL
      AND is_overdue = 0
      AND sla_deadline < @now_utc;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 32. Số sự cố đang xử lý (status 1, 2) của từng cán bộ (danh sách '1,2') */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_CountOpenByOfficers
    @officer_ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.assigned_officer_id AS officer_id, COUNT(*) AS open_count
    FROM tbl_incidents i
    WHERE i.status IN (1, 2)
      AND i.assigned_officer_id IS NOT NULL
      AND EXISTS (SELECT 1 FROM STRING_SPLIT(@officer_ids, ',') s
                  WHERE TRY_CAST(s.value AS INT) = i.assigned_officer_id)
    GROUP BY i.assigned_officer_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 33. Chuyển đơn vị của sự cố khi gộp/sáp nhập đơn vị. Trả số dòng (không NOCOUNT). */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_RepointAdminUnit
    @from_unit_id INT,
    @to_unit_id   INT,
    @now_utc      DATETIME2
AS
BEGIN
    UPDATE tbl_incidents
    SET admin_unit_id = @to_unit_id,
        updated_at    = @now_utc
    WHERE admin_unit_id = @from_unit_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 34. Ảnh của sự cố */
CREATE OR ALTER PROCEDURE SP_tbl_incident_images_ReadByIncident
    @incident_id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, image_type, image_url, capture_point.STAsText() AS capture_point_wkt,
           uploaded_by_user_id, uploaded_at
    FROM tbl_incident_images
    WHERE incident_id = @incident_id
    ORDER BY id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 35. Nhật ký xử lý của sự cố */
CREATE OR ALTER PROCEDURE SP_tbl_incident_logs_ReadByIncident
    @incident_id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.id, l.incident_id, l.action_type, l.action_note, l.old_status, l.new_status,
           l.action_by_user_id, u.full_name AS action_by_name, l.created_at
    FROM tbl_incident_logs l
    LEFT JOIN tbl_users u ON u.id = l.action_by_user_id
    WHERE l.incident_id = @incident_id
    ORDER BY l.created_at, l.id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 36. Đánh giá của công dân cho sự cố */
CREATE OR ALTER PROCEDURE SP_tbl_incident_ratings_ReadByIncident
    @incident_id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, incident_id, rating, feedback, created_at
    FROM tbl_incident_ratings
    WHERE incident_id = @incident_id;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   V. Thông báo (muc 4.4)
   ===================================================================== */

/* 37. Danh sách thông báo của người dùng, có phân trang */
CREATE OR ALTER PROCEDURE SP_tbl_notifications_List
    @user_id     INT,
    @unread_only BIT,
    @page_index  INT,
    @page_size   INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ps   INT = CASE WHEN @page_size IS NULL OR @page_size < 1 THEN 20
                             WHEN @page_size > 200 THEN 200 ELSE @page_size END;
    DECLARE @pi   INT = CASE WHEN @page_index IS NULL OR @page_index < 1 THEN 1 ELSE @page_index END;
    DECLARE @skip INT = (@pi - 1) * @ps;

    SELECT n.id, n.user_id, n.incident_id, n.notification_type, n.title, n.content,
           n.is_read, n.created_at, n.read_at
    FROM tbl_notifications n
    WHERE n.user_id = @user_id
      AND (ISNULL(@unread_only, 0) = 0 OR n.is_read = 0)
    ORDER BY n.created_at DESC, n.id DESC
    OFFSET @skip ROWS FETCH NEXT @ps ROWS ONLY;

    SELECT COUNT(*) AS total_count
    FROM tbl_notifications n
    WHERE n.user_id = @user_id
      AND (ISNULL(@unread_only, 0) = 0 OR n.is_read = 0);
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 38. Số thông báo chưa đọc */
CREATE OR ALTER PROCEDURE SP_tbl_notifications_CountUnread
    @user_id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS unread_count
    FROM tbl_notifications
    WHERE user_id = @user_id AND is_read = 0;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 39. Đánh dấu đã đọc một thông báo của đúng người dùng. Trả số dòng (không NOCOUNT). */
CREATE OR ALTER PROCEDURE SP_tbl_notifications_MarkRead
    @id      BIGINT,
    @user_id INT,
    @read_at DATETIME2
AS
BEGIN
    UPDATE tbl_notifications
    SET is_read = 1, read_at = @read_at
    WHERE id = @id AND user_id = @user_id AND is_read = 0;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 40. Đánh dấu đã đọc toàn bộ. Trả số dòng (không NOCOUNT). */
CREATE OR ALTER PROCEDURE SP_tbl_notifications_MarkAllRead
    @user_id INT,
    @read_at DATETIME2
AS
BEGIN
    UPDATE tbl_notifications
    SET is_read = 1, read_at = @read_at
    WHERE user_id = @user_id AND is_read = 0;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   W. Refresh token (muc 4.1, 11-12)
   ===================================================================== */

/* 11. Refresh token theo hash, kèm trạng thái người dùng */
CREATE OR ALTER PROCEDURE SP_tbl_refresh_tokens_ReadByHash
    @token_hash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT rt.id, rt.user_id, rt.token_hash, rt.expires_at, rt.created_at, rt.created_by_ip,
           rt.revoked_at, rt.replaced_by_token_id, rt.security_stamp,
           u.is_active AS user_is_active, u.security_stamp AS user_security_stamp
    FROM tbl_refresh_tokens rt
    JOIN tbl_users u ON u.id = rt.user_id
    WHERE rt.token_hash = @token_hash;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 12. Thu hồi mọi refresh token chưa thu hồi của người dùng. Trả số dòng (không NOCOUNT). */
CREATE OR ALTER PROCEDURE SP_tbl_refresh_tokens_RevokeByUser
    @user_id    INT,
    @revoked_at DATETIME2
AS
BEGIN
    UPDATE tbl_refresh_tokens
    SET revoked_at = @revoked_at
    WHERE user_id = @user_id AND revoked_at IS NULL;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   X. Báo cáo và dashboard (muc 4.5)
   Quy ước: báo cáo chỉ tính sự cố gốc (parent_incident_id IS NULL) trừ khi ghi khác.
   Phạm vi lọc theo tbl_admin_units.unit_path của sự cố (admin_unit_id).
   ===================================================================== */

/* 41. Số liệu tổng quan dashboard */
CREATE OR ALTER PROCEDURE SP_rpt_dashboard_Summary
    @scope_paths    NVARCHAR(MAX) = NULL,
    @unit_path      VARCHAR(400) = NULL,
    @today_start_utc DATETIME2,
    @since_utc      DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH sc AS (
        SELECT i.id, i.parent_incident_id, i.status, i.is_overdue, i.is_manual_review,
               i.created_at, i.resolved_at, i.sla_deadline
        FROM tbl_incidents i
        LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
        WHERE (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                               WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
          AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
    )
    SELECT
        COUNT(CASE WHEN sc.parent_incident_id IS NULL AND sc.status IN (0, 1, 2) THEN 1 END) AS open_count,
        COUNT(CASE WHEN sc.parent_incident_id IS NULL AND sc.status IN (0, 1, 2) AND sc.is_overdue = 1 THEN 1 END) AS overdue_count,
        COUNT(CASE WHEN sc.parent_incident_id IS NULL AND sc.status IN (0, 1, 2) AND sc.is_manual_review = 1 THEN 1 END) AS pending_review_count,
        COUNT(CASE WHEN sc.parent_incident_id IS NULL AND sc.status = 3 AND sc.resolved_at >= @today_start_utc THEN 1 END) AS resolved_today_count,
        COUNT(CASE WHEN sc.parent_incident_id IS NULL AND sc.created_at >= @today_start_utc THEN 1 END) AS created_today_count,
        (SELECT AVG(CAST(DATEDIFF(SECOND, s.created_at, d.first_dispatch) AS FLOAT)) / 60.0
         FROM sc s
         CROSS APPLY (SELECT MIN(l.created_at) AS first_dispatch
                      FROM tbl_incident_logs l
                      WHERE l.incident_id = s.id AND l.action_type = 'DISPATCHED') d
         WHERE s.parent_incident_id IS NULL
           AND s.created_at >= @since_utc
           AND d.first_dispatch IS NOT NULL) AS avg_dispatch_minutes,
        (SELECT AVG(CAST(DATEDIFF(MINUTE, s.created_at, s.resolved_at) AS FLOAT)) / 60.0
         FROM sc s
         WHERE s.parent_incident_id IS NULL
           AND s.status = 3
           AND s.resolved_at IS NOT NULL
           AND s.created_at >= @since_utc) AS avg_resolution_hours,
        (SELECT CASE WHEN COUNT(*) = 0 THEN 0.0
                     ELSE CAST(SUM(CASE WHEN s.parent_incident_id IS NOT NULL THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) END
         FROM sc s
         WHERE s.created_at >= @since_utc) AS duplicate_rate,
        (SELECT CASE WHEN COUNT(*) = 0 THEN NULL
                     ELSE CAST(SUM(CASE WHEN s.resolved_at <= s.sla_deadline THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) END
         FROM sc s
         WHERE s.parent_incident_id IS NULL
           AND s.status = 3
           AND s.sla_deadline IS NOT NULL
           AND s.resolved_at IS NOT NULL
           AND s.created_at >= @since_utc) AS on_time_rate
    FROM sc;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 42. Điểm nóng: tọa độ sự cố gốc, bỏ CANCELLED */
CREATE OR ALTER PROCEDURE SP_rpt_dashboard_Heatmap
    @scope_paths  NVARCHAR(MAX) = NULL,
    @unit_path    VARCHAR(400) = NULL,
    @from_utc     DATETIME2 = NULL,
    @to_utc       DATETIME2 = NULL,
    @category_id  INT = NULL,
    @take         INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @top INT = ISNULL(@take, 1000);

    SELECT TOP (@top)
           i.location_point.Lat  AS latitude,
           i.location_point.Long AS longitude,
           i.duplicate_count
    FROM tbl_incidents i
    LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
    WHERE i.parent_incident_id IS NULL
      AND i.status <> 4
      AND (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                           WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
      AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
      AND (@from_utc IS NULL OR i.created_at >= @from_utc)
      AND (@to_utc IS NULL OR i.created_at < @to_utc)
      AND (@category_id IS NULL OR i.category_id = @category_id)
    ORDER BY i.created_at DESC, i.id DESC;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 43. Xu hướng theo ngày giờ Việt Nam (UTC+7). Cửa sổ [@start_utc, @start_utc + @days). */
CREATE OR ALTER PROCEDURE SP_rpt_dashboard_Trend
    @scope_paths NVARCHAR(MAX) = NULL,
    @unit_path   VARCHAR(400) = NULL,
    @start_utc   DATETIME2,
    @days        INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @end_utc DATETIME2 = DATEADD(DAY, @days, @start_utc);

    SELECT x.day_vn,
           SUM(x.created_count)  AS created_count,
           SUM(x.resolved_count) AS resolved_count
    FROM (
        SELECT CAST(DATEADD(HOUR, 7, i.created_at) AS DATE) AS day_vn, 1 AS created_count, 0 AS resolved_count
        FROM tbl_incidents i
        LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
        WHERE i.parent_incident_id IS NULL
          AND i.created_at >= @start_utc AND i.created_at < @end_utc
          AND (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                               WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
          AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
        UNION ALL
        SELECT CAST(DATEADD(HOUR, 7, i.resolved_at) AS DATE), 0, 1
        FROM tbl_incidents i
        LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
        WHERE i.parent_incident_id IS NULL
          AND i.status = 3
          AND i.resolved_at >= @start_utc AND i.resolved_at < @end_utc
          AND (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                               WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
          AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
    ) x
    GROUP BY x.day_vn
    ORDER BY x.day_vn;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 44. Thống kê theo đơn vị con trực tiếp (đơn vị lá trong cây được cộng vào) */
CREATE OR ALTER PROCEDURE SP_rpt_by_child_units
    @unit_id  INT,
    @from_utc DATETIME2 = NULL,
    @to_utc   DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.id AS unit_id, c.unit_code, c.name,
           COUNT(i.id) AS total_incidents,
           COUNT(CASE WHEN i.status IN (0, 1, 2) THEN 1 END) AS open_incidents,
           COUNT(CASE WHEN i.status = 3 THEN 1 END) AS resolved_incidents,
           COUNT(CASE WHEN i.status IN (0, 1, 2) AND i.is_overdue = 1 THEN 1 END) AS overdue_incidents
    FROM tbl_admin_units c
    LEFT JOIN tbl_admin_units leaf ON leaf.unit_path LIKE c.unit_path + '%'
    LEFT JOIN tbl_incidents i      ON i.admin_unit_id = leaf.id
                                  AND i.parent_incident_id IS NULL
                                  AND (@from_utc IS NULL OR i.created_at >= @from_utc)
                                  AND (@to_utc IS NULL OR i.created_at < @to_utc)
    WHERE c.parent_id = @unit_id AND c.is_active = 1
    GROUP BY c.id, c.unit_code, c.name
    ORDER BY c.name;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 45. Thống kê theo danh mục */
CREATE OR ALTER PROCEDURE SP_rpt_by_category
    @scope_paths NVARCHAR(MAX) = NULL,
    @unit_path   VARCHAR(400) = NULL,
    @from_utc    DATETIME2 = NULL,
    @to_utc      DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.category_id,
           ISNULL(c.category_name, N'Chưa phân loại') AS category_name,
           COUNT(*) AS total,
           COUNT(CASE WHEN i.status IN (0, 1, 2) THEN 1 END) AS [open],
           COUNT(CASE WHEN i.status = 3 THEN 1 END) AS resolved,
           COUNT(CASE WHEN i.status IN (0, 1, 2) AND i.is_overdue = 1 THEN 1 END) AS overdue
    FROM tbl_incidents i
    LEFT JOIN tbl_categories c   ON c.id = i.category_id
    LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
    WHERE i.parent_incident_id IS NULL
      AND (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                           WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
      AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
      AND (@from_utc IS NULL OR i.created_at >= @from_utc)
      AND (@to_utc IS NULL OR i.created_at < @to_utc)
    GROUP BY i.category_id, c.category_name
    ORDER BY total DESC, category_name;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* 46. KPI cán bộ theo sự cố gốc được giao */
CREATE OR ALTER PROCEDURE SP_rpt_officer_kpi
    @scope_paths NVARCHAR(MAX) = NULL,
    @unit_path   VARCHAR(400) = NULL,
    @from_utc    DATETIME2 = NULL,
    @to_utc      DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH sc AS (
        SELECT i.id, i.assigned_officer_id, i.status, i.created_at, i.accepted_at, i.resolved_at,
               i.sla_deadline, i.escalated_at, p.sla_hours
        FROM tbl_incidents i
        LEFT JOIN tbl_admin_units au ON au.id = i.admin_unit_id
        LEFT JOIN tbl_sla_policies p ON p.id = i.sla_policy_id
        WHERE i.parent_incident_id IS NULL
          AND i.assigned_officer_id IS NOT NULL
          AND (@scope_paths IS NULL OR EXISTS (SELECT 1 FROM STRING_SPLIT(@scope_paths, ',') s
                                               WHERE s.value <> N'' AND au.unit_path LIKE s.value + '%'))
          AND (@unit_path IS NULL OR au.unit_path LIKE @unit_path + '%')
          AND (@from_utc IS NULL OR i.created_at >= @from_utc)
          AND (@to_utc IS NULL OR i.created_at < @to_utc)
    ),
    rt AS (
        SELECT s.assigned_officer_id AS officer_id, AVG(CAST(r.rating AS FLOAT)) AS avg_rating
        FROM sc s
        JOIN tbl_incident_ratings r ON r.incident_id = s.id
        GROUP BY s.assigned_officer_id
    )
    SELECT s.assigned_officer_id AS officer_id,
           u.full_name,
           COUNT(*) AS assigned,
           COUNT(CASE WHEN s.status = 3 THEN 1 END) AS resolved,
           COUNT(CASE WHEN s.status = 3 AND s.sla_deadline IS NOT NULL THEN 1 END) AS resolved_with_sla,
           COUNT(CASE WHEN s.status = 3 AND s.sla_deadline IS NOT NULL AND s.resolved_at <= s.sla_deadline THEN 1 END) AS on_time_count,
           COUNT(CASE WHEN s.accepted_at IS NOT NULL AND s.sla_deadline IS NOT NULL THEN 1 END) AS accepted_with_sla,
           CAST(AVG(CASE WHEN s.accepted_at IS NOT NULL AND s.sla_deadline IS NOT NULL AND s.sla_hours IS NOT NULL
                         THEN CAST(DATEDIFF(SECOND, DATEADD(HOUR, -s.sla_hours, s.sla_deadline), s.accepted_at) AS FLOAT) / 60.0
                    END) AS FLOAT) AS avg_accept_minutes,
           CAST(AVG(CASE WHEN s.status = 3 AND s.resolved_at IS NOT NULL
                         THEN CAST(DATEDIFF(MINUTE, s.created_at, s.resolved_at) AS FLOAT) / 60.0
                    END) AS FLOAT) AS avg_resolve_hours,
           MAX(rt.avg_rating) AS avg_rating,
           COUNT(CASE WHEN s.escalated_at IS NOT NULL THEN 1 END) AS escalated_count,
           COUNT(CASE WHEN s.status IN (1, 2) THEN 1 END) AS open_now
    FROM sc s
    JOIN tbl_users u ON u.id = s.assigned_officer_id
    LEFT JOIN rt ON rt.officer_id = s.assigned_officer_id
    GROUP BY s.assigned_officer_id, u.full_name
    ORDER BY u.full_name;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* =====================================================================
   BO SUNG: xem phan phan anh cua nguoi gui, gioi han theo so dien thoai, gan phan anh an danh khi dang ky
   ===================================================================== */
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* Phan anh cua nguoi dung: theo user_id hoac so dien thoai (toi da 200, moi nhat truoc) */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_ListByReporter
    @user_id INT = NULL,
    @phone   VARCHAR(15) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (200)
        i.tracking_code,
        i.status,
        c.category_name,
        i.address_text,
        i.created_at,
        i.resolved_at
    FROM tbl_incidents i
    LEFT JOIN tbl_categories c ON c.id = i.category_id
    WHERE (@user_id IS NOT NULL AND i.reporter_user_id = @user_id)
       OR (@phone IS NOT NULL AND i.reporter_phone = @phone)
    ORDER BY i.created_at DESC;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* So phan anh cua mot so dien thoai tu thoi diem @since_utc (gioi han gui) */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_CountRecentByPhone
    @phone     VARCHAR(15),
    @since_utc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS total_count
    FROM tbl_incidents
    WHERE reporter_phone = @phone
      AND created_at >= @since_utc;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO

/* Gan cac phan anh an danh cung so dien thoai cho tai khoan vua dang ky (khong SET NOCOUNT de tra so dong) */
CREATE OR ALTER PROCEDURE SP_tbl_incidents_LinkReporterByPhone
    @phone   VARCHAR(15),
    @user_id INT
AS
BEGIN
    UPDATE tbl_incidents
    SET reporter_user_id = @user_id,
        updated_at = SYSUTCDATETIME()
    WHERE reporter_phone = @phone
      AND reporter_user_id IS NULL;
END;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO



/* =====================================================================
   6. Du lieu test (anh chup database dev luc 2026-10-13T08:59:13 UTC)
   ===================================================================== */
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_admin_levels (3 dong) ---------- */
INSERT INTO dbo.tbl_admin_levels ([id], [level_code], [level_name], [level_order]) VALUES
 (1, 'PROVINCE', N'Tỉnh / Thành phố trực thuộc TW', 1),
 (2, 'COMMUNE', N'Phường / Xã / Đặc khu', 2),
 (3, 'HAMLET', N'Khu phố / Thôn / Ấp / Tổ dân phố', 3);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_admin_unit_types (9 dong) ---------- */
INSERT INTO dbo.tbl_admin_unit_types ([id], [type_code], [type_name], [level_id]) VALUES
 (1, 'TINH', N'Tỉnh', 1),
 (2, 'THANH_PHO', N'Thành phố', 1),
 (3, 'PHUONG', N'Phường', 2),
 (4, 'XA', N'Xã', 2),
 (5, 'DAC_KHU', N'Đặc khu', 2),
 (6, 'KHU_PHO', N'Khu phố', 3),
 (7, 'THON', N'Thôn', 3),
 (8, 'AP', N'Ấp', 3),
 (9, 'TO_DAN_PHO', N'Tổ dân phố', 3);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_admin_units (48 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_admin_units ON;
INSERT INTO dbo.tbl_admin_units ([id], [unit_code], [name], [unit_type_id], [parent_id], [boundary], [is_active], [effective_from], [effective_to], [merged_into_unit_id], [created_at], [updated_at]) VALUES
 (1, 'DNI', N'Thành phố Đồng Nai', 2, NULL, geography::STGeomFromText('POLYGON ((106.73 10.75, 106.82 10.62, 107.02 10.58, 107.28 10.65, 107.48 10.78, 107.55 10.98, 107.4 11.22, 107.45 11.45, 107.3 11.62, 107.4 11.82, 107.3 12.05, 107.1 12.2, 106.85 12.08, 106.62 11.95, 106.48 11.78, 106.45 11.55, 106.58 11.38, 106.8 11.32, 106.98 11.18, 106.77 11.06, 106.765 10.97, 106.76 10.88, 106.73 10.75))', 4326), 1, '2026-04-30', NULL, NULL, '2026-10-11T08:12:00', NULL);
INSERT INTO dbo.tbl_admin_units ([id], [unit_code], [name], [unit_type_id], [parent_id], [boundary], [is_active], [effective_from], [effective_to], [merged_into_unit_id], [created_at], [updated_at]) VALUES
 (2, 'DNI-BH', N'Phường Biên Hòa', 3, 1, geography::STGeomFromText('POLYGON ((106.775 10.955, 106.78 10.93, 106.795 10.912, 106.815 10.898, 106.835 10.9, 106.832 10.915, 106.82 10.928, 106.812 10.94, 106.8 10.95, 106.775 10.955))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (3, 'DNI-HN', N'Phường Hố Nai', 3, 1, geography::STGeomFromText('POLYGON ((106.885 10.978, 106.9 10.972, 106.91 10.955, 106.935 10.95, 106.955 10.965, 106.95 10.995, 106.92 11, 106.895 11.02, 106.885 10.995, 106.885 10.978))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (4, 'DNI-LB', N'Phường Long Bình', 3, 1, geography::STGeomFromText('POLYGON ((106.86 10.968, 106.868 10.95, 106.872 10.928, 106.862 10.915, 106.88 10.912, 106.905 10.925, 106.91 10.955, 106.9 10.972, 106.885 10.978, 106.872 10.975, 106.86 10.968))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (5, 'DNI-LH', N'Phường Long Hưng', 3, 1, geography::STGeomFromText('POLYGON ((106.84 10.89, 106.85 10.87, 106.86 10.855, 106.885 10.85, 106.895 10.87, 106.895 10.9, 106.88 10.912, 106.862 10.915, 106.845 10.905, 106.84 10.89))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (6, 'DNI-PT', N'Phường Phước Tân', 3, 1, geography::STGeomFromText('POLYGON ((106.88 10.912, 106.895 10.9, 106.93 10.88, 106.96 10.9, 106.955 10.935, 106.935 10.95, 106.91 10.955, 106.905 10.925, 106.88 10.912))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (7, 'DNI-TB', N'Phường Trấn Biên', 3, 1, geography::STGeomFromText('POLYGON ((106.79 10.972, 106.8 10.95, 106.812 10.94, 106.82 10.928, 106.832 10.915, 106.845 10.905, 106.862 10.915, 106.855 10.93, 106.845 10.94, 106.838 10.952, 106.832 10.962, 106.815 10.968, 106.79 10.972))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (8, 'DNI-TD', N'Phường Trảng Dài', 3, 1, geography::STGeomFromText('POLYGON ((106.845 10.968, 106.86 10.968, 106.872 10.975, 106.885 10.978, 106.885 10.995, 106.895 11.02, 106.88 11.05, 106.835 11.045, 106.84 11.01, 106.85 10.985, 106.845 10.968))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (9, 'DNI-TP', N'Phường Tam Phước', 3, 1, geography::STGeomFromText('POLYGON ((106.885 10.85, 106.9 10.835, 106.94 10.84, 106.955 10.865, 106.93 10.88, 106.895 10.9, 106.895 10.87, 106.885 10.85))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (10, 'DNI-TT', N'Phường Tân Triều', 3, 1, geography::STGeomFromText('POLYGON ((106.783 10.985, 106.79 10.972, 106.815 10.968, 106.832 10.962, 106.845 10.968, 106.85 10.985, 106.84 11.01, 106.835 11.045, 106.8 11.04, 106.78 11.01, 106.783 10.985))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL),
 (11, 'DNI-TH', N'Phường Tam Hiệp', 3, 1, geography::STGeomFromText('POLYGON ((106.832 10.962, 106.838 10.952, 106.845 10.94, 106.855 10.93, 106.862 10.915, 106.872 10.928, 106.868 10.95, 106.86 10.968, 106.845 10.968, 106.832 10.962))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:00', NULL);
INSERT INTO dbo.tbl_admin_units ([id], [unit_code], [name], [unit_type_id], [parent_id], [boundary], [is_active], [effective_from], [effective_to], [merged_into_unit_id], [created_at], [updated_at]) VALUES
 (12, 'DNI-BH-KP01', N'Khu phố 1', 6, 2, geography::STGeomFromText('POLYGON ((106.77720000000001 10.944, 106.788 10.944, 106.788 10.9524, 106.775 10.955, 106.77720000000001 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (13, 'DNI-BH-KP02', N'Khu phố 2', 6, 2, geography::STGeomFromText('POLYGON ((106.788 10.944, 106.8 10.944, 106.8 10.95, 106.788 10.9524, 106.788 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (14, 'DNI-BH-KP03', N'Khu phố 3', 6, 2, geography::STGeomFromText('POLYGON ((106.7796 10.932, 106.788 10.932, 106.788 10.944, 106.77720000000001 10.944, 106.7796 10.932))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (15, 'DNI-BH-KP04', N'Khu phố 4', 6, 2, geography::STGeomFromText('POLYGON ((106.788 10.932, 106.8 10.932, 106.8 10.944, 106.788 10.944, 106.788 10.932))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (16, 'DNI-BH-KP05', N'Khu phố 5', 6, 2, geography::STGeomFromText('POLYGON ((106.8 10.932, 106.812 10.932, 106.81733333333332 10.932, 106.812 10.94, 106.8 10.95, 106.8 10.944, 106.8 10.932))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (17, 'DNI-BH-KP06', N'Khu phố 6', 6, 2, geography::STGeomFromText('POLYGON ((106.78833333333334 10.92, 106.8 10.92, 106.8 10.932, 106.788 10.932, 106.7796 10.932, 106.78 10.93, 106.78833333333334 10.92))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (18, 'DNI-BH-KP07', N'Khu phố 7', 6, 2, geography::STGeomFromText('POLYGON ((106.8 10.92, 106.812 10.92, 106.812 10.932, 106.8 10.932, 106.8 10.92))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (19, 'DNI-BH-KP08', N'Khu phố 8', 6, 2, geography::STGeomFromText('POLYGON ((106.812 10.92, 106.824 10.92, 106.8273846153846 10.92, 106.82 10.928, 106.81733333333332 10.932, 106.812 10.932, 106.812 10.92))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (20, 'DNI-BH-KP09', N'Khu phố 9', 6, 2, geography::STGeomFromText('POLYGON ((106.8 10.908500000000004, 106.8 10.92, 106.78833333333334 10.92, 106.795 10.912, 106.8 10.908500000000004))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (21, 'DNI-BH-KP10', N'Khu phố 10', 6, 2, geography::STGeomFromText('POLYGON ((106.812 10.9001, 106.812 10.908, 106.812 10.92, 106.8 10.92, 106.8 10.908500000000004, 106.812 10.9001))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (22, 'DNI-BH-KP11', N'Khu phố 11', 6, 2, geography::STGeomFromText('POLYGON ((106.812 10.908, 106.824 10.908, 106.824 10.92, 106.812 10.92, 106.812 10.908))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (23, 'DNI-BH-KP12', N'Khu phố 12', 6, 2, geography::STGeomFromText('POLYGON ((106.824 10.908, 106.8334 10.908, 106.832 10.915, 106.8273846153846 10.92, 106.824 10.92, 106.824 10.908))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (24, 'DNI-BH-KP13', N'Khu phố 13', 6, 2, geography::STGeomFromText('POLYGON ((106.815 10.898, 106.824 10.898900000000001, 106.824 10.908, 106.812 10.908, 106.812 10.9001, 106.815 10.898))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (25, 'DNI-BH-KP14', N'Khu phố 14', 6, 2, geography::STGeomFromText('POLYGON ((106.824 10.898900000000001, 106.835 10.9, 106.8334 10.908, 106.824 10.908, 106.824 10.898900000000001))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (26, 'DNI-TB-KP01', N'Khu phố 1', 6, 7, geography::STGeomFromText('POLYGON ((106.79727272727273 10.956, 106.8 10.956, 106.8 10.968, 106.8 10.9704, 106.79 10.972, 106.79727272727273 10.956))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (27, 'DNI-TB-KP02', N'Khu phố 2', 6, 7, geography::STGeomFromText('POLYGON ((106.8 10.956, 106.812 10.956, 106.812 10.968, 106.812 10.96848, 106.8 10.9704, 106.8 10.968, 106.8 10.956))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (28, 'DNI-TB-KP03', N'Khu phố 3', 6, 7, geography::STGeomFromText('POLYGON ((106.812 10.956, 106.824 10.956, 106.824 10.964823529411763, 106.815 10.968, 106.812 10.96848, 106.812 10.968, 106.812 10.956))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (29, 'DNI-TB-KP04', N'Khu phố 4', 6, 7, geography::STGeomFromText('POLYGON ((106.824 10.956, 106.8356 10.956, 106.832 10.962, 106.824 10.964823529411763, 106.824 10.956))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (30, 'DNI-TB-KP05', N'Khu phố 5', 6, 7, geography::STGeomFromText('POLYGON ((106.8072 10.944, 106.812 10.944, 106.812 10.956, 106.8 10.956, 106.79727272727273 10.956, 106.8 10.95, 106.8072 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (31, 'DNI-TB-KP06', N'Khu phố 6', 6, 7, geography::STGeomFromText('POLYGON ((106.812 10.944, 106.824 10.944, 106.824 10.956, 106.812 10.956, 106.812 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (32, 'DNI-TB-KP07', N'Khu phố 7', 6, 7, geography::STGeomFromText('POLYGON ((106.824 10.944, 106.836 10.944, 106.84266666666666 10.944, 106.838 10.952, 106.8356 10.956, 106.824 10.956, 106.824 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (33, 'DNI-TB-KP08', N'Khu phố 8', 6, 7, geography::STGeomFromText('POLYGON ((106.81733333333332 10.932, 106.824 10.932, 106.824 10.944, 106.812 10.944, 106.8072 10.944, 106.812 10.94, 106.81733333333332 10.932))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (34, 'DNI-TB-KP09', N'Khu phố 9', 6, 7, geography::STGeomFromText('POLYGON ((106.824 10.932, 106.836 10.932, 106.836 10.944, 106.824 10.944, 106.824 10.932))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (35, 'DNI-TB-KP10', N'Khu phố 10', 6, 7, geography::STGeomFromText('POLYGON ((106.836 10.932, 106.848 10.932, 106.85300000000001 10.932, 106.845 10.94, 106.84266666666666 10.944, 106.836 10.944, 106.836 10.932))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (36, 'DNI-TB-KP11', N'Khu phố 11', 6, 7, geography::STGeomFromText('POLYGON ((106.836 10.911923076923074, 106.836 10.92, 106.836 10.932, 106.824 10.932, 106.81733333333332 10.932, 106.82 10.928, 106.832 10.915, 106.836 10.911923076923074))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (37, 'DNI-TB-KP12', N'Khu phố 12', 6, 7, geography::STGeomFromText('POLYGON ((106.836 10.92, 106.848 10.92, 106.848 10.932, 106.836 10.932, 106.836 10.92))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (38, 'DNI-TB-KP13', N'Khu phố 13', 6, 7, geography::STGeomFromText('POLYGON ((106.848 10.92, 106.85966666666667 10.92, 106.855 10.93, 106.85300000000001 10.932, 106.848 10.932, 106.848 10.92))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (39, 'DNI-TB-KP14', N'Khu phố 14', 6, 7, geography::STGeomFromText('POLYGON ((106.845 10.905, 106.848 10.906764705882352, 106.848 10.908, 106.848 10.92, 106.836 10.92, 106.836 10.911923076923074, 106.845 10.905))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (40, 'DNI-TB-KP15', N'Khu phố 15', 6, 7, geography::STGeomFromText('POLYGON ((106.848 10.906764705882352, 106.862 10.915, 106.85966666666667 10.92, 106.848 10.92, 106.848 10.908, 106.848 10.906764705882352))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (41, 'DNI-TH-KP01', N'Khu phố 1', 6, 11, geography::STGeomFromText('POLYGON ((106.8356 10.956, 106.836 10.956, 106.848 10.956, 106.848 10.968, 106.845 10.968, 106.832 10.962, 106.8356 10.956))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (42, 'DNI-TH-KP02', N'Khu phố 2', 6, 11, geography::STGeomFromText('POLYGON ((106.848 10.956, 106.86 10.956, 106.86533333333333 10.956, 106.86 10.968, 106.848 10.968, 106.848 10.956))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (43, 'DNI-TH-KP03', N'Khu phố 3', 6, 11, geography::STGeomFromText('POLYGON ((106.84266666666666 10.944, 106.848 10.944, 106.848 10.956, 106.836 10.956, 106.836 10.955333333333325, 106.838 10.952, 106.84266666666666 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (44, 'DNI-TH-KP04', N'Khu phố 4', 6, 11, geography::STGeomFromText('POLYGON ((106.848 10.944, 106.86 10.944, 106.86 10.956, 106.848 10.956, 106.848 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (45, 'DNI-TH-KP05', N'Khu phố 5', 6, 11, geography::STGeomFromText('POLYGON ((106.86 10.944, 106.8690909090909 10.944, 106.868 10.95, 106.86533333333333 10.956, 106.86 10.956, 106.86 10.944))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (46, 'DNI-TH-KP06', N'Khu phố 6', 6, 11, geography::STGeomFromText('POLYGON ((106.85966666666667 10.92, 106.86 10.92, 106.86 10.932, 106.86 10.944, 106.848 10.944, 106.84266666666666 10.944, 106.845 10.94, 106.855 10.93, 106.85966666666667 10.92))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (47, 'DNI-TH-KP07', N'Khu phố 7', 6, 11, geography::STGeomFromText('POLYGON ((106.86 10.932, 106.87127272727272 10.932, 106.8690909090909 10.944, 106.86 10.944, 106.86 10.932))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL),
 (48, 'DNI-TH-KP08', N'Khu phố 8', 6, 11, geography::STGeomFromText('POLYGON ((106.862 10.915, 106.872 10.928, 106.87127272727272 10.932, 106.86 10.932, 106.86 10.92, 106.86 10.91928571428571, 106.862 10.915))', 4326), 1, '2025-07-01', NULL, NULL, '2026-10-11T08:12:01', NULL);
SET IDENTITY_INSERT dbo.tbl_admin_units OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_roles (5 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_roles ON;
INSERT INTO dbo.tbl_roles ([id], [role_code], [role_name], [description], [is_system], [is_active], [created_at]) VALUES
 (1, 'CITIZEN', N'Công dân', N'Gửi phản ánh, theo dõi và đánh giá sự cố của mình', 1, 1, '2026-10-10T03:00:50'),
 (2, 'GRID_OFFICER', N'Cán bộ ô lưới', N'Nhận và xử lý sự cố trong phạm vi được giao', 1, 1, '2026-10-10T03:00:50'),
 (3, 'DISPATCHER', N'Điều phối viên', N'Xử lý ngoại lệ, phân loại và điều phối thủ công', 1, 1, '2026-10-10T03:00:50'),
 (4, 'LEADER', N'Lãnh đạo', N'Giám sát, nhận leo thang, xem báo cáo và KPI', 1, 1, '2026-10-10T03:00:50'),
 (5, 'SYSTEM_ADMIN', N'Quản trị hệ thống', N'Toàn quyền cấu hình hệ thống', 1, 1, '2026-10-10T03:00:50');
SET IDENTITY_INSERT dbo.tbl_roles OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_permissions (26 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_permissions ON;
INSERT INTO dbo.tbl_permissions ([id], [permission_code], [permission_name], [module]) VALUES
 (1, 'incident.create', N'Gửi phản ánh', 'incident'),
 (2, 'incident.view_own', N'Xem phản ánh của chính mình', 'incident'),
 (3, 'incident.rate', N'Đánh giá kết quả xử lý', 'incident'),
 (4, 'incident.view', N'Xem sự cố trong phạm vi', 'incident'),
 (5, 'incident.accept', N'Nhận việc', 'incident'),
 (6, 'incident.resolve', N'Nghiệm thu, đóng sự cố', 'incident'),
 (7, 'incident.classify', N'Phân loại thủ công', 'incident'),
 (8, 'incident.dispatch', N'Điều phối thủ công', 'incident'),
 (9, 'incident.reassign', N'Giao lại / chuyển cấp', 'incident'),
 (10, 'incident.cancel', N'Hủy sự cố', 'incident'),
 (11, 'incident.receive_escalation', N'Nhận cảnh báo leo thang SLA', 'incident'),
 (12, 'grid.view', N'Xem ô lưới', 'grid'),
 (13, 'grid.manage', N'Thêm, sửa, xóa ô lưới', 'grid'),
 (14, 'grid.assign_officer', N'Phân công cán bộ ô lưới', 'grid'),
 (15, 'admin_unit.view', N'Xem đơn vị hành chính', 'admin_unit'),
 (16, 'admin_unit.manage', N'Quản lý đơn vị hành chính, sáp nhập', 'admin_unit'),
 (17, 'dashboard.view', N'Xem bản đồ điều hành, heatmap', 'report'),
 (18, 'report.view', N'Xem báo cáo', 'report'),
 (19, 'report.export', N'Xuất báo cáo', 'report'),
 (20, 'kpi.view', N'Xem KPI cán bộ', 'report'),
 (21, 'user.view', N'Xem người dùng', 'user'),
 (22, 'user.manage', N'Quản lý người dùng, gán vai trò', 'user'),
 (23, 'role.manage', N'Quản lý vai trò và quyền', 'user'),
 (24, 'category.manage', N'Quản lý danh mục sự cố', 'config'),
 (25, 'sla.manage', N'Quản lý chính sách SLA', 'config'),
 (27, 'incident.delete', N'Xóa phản ánh (không cần lý do)', 'incident');
SET IDENTITY_INSERT dbo.tbl_permissions OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_role_permissions (53 dong) ---------- */
INSERT INTO dbo.tbl_role_permissions ([role_id], [permission_id]) VALUES
 (1, 1),
 (1, 2),
 (1, 3),
 (2, 4),
 (2, 5),
 (2, 6),
 (2, 12),
 (2, 15),
 (3, 4),
 (3, 7),
 (3, 8),
 (3, 9),
 (3, 10),
 (3, 12),
 (3, 15),
 (3, 17),
 (4, 4),
 (4, 9),
 (4, 11),
 (4, 12),
 (4, 14),
 (4, 15),
 (4, 17),
 (4, 18),
 (4, 19),
 (4, 20),
 (4, 21),
 (5, 1),
 (5, 2),
 (5, 3),
 (5, 4),
 (5, 5),
 (5, 6),
 (5, 7),
 (5, 8),
 (5, 9),
 (5, 10),
 (5, 11),
 (5, 12),
 (5, 13),
 (5, 14),
 (5, 15),
 (5, 16),
 (5, 17),
 (5, 18),
 (5, 19),
 (5, 20),
 (5, 21),
 (5, 22),
 (5, 23),
 (5, 24),
 (5, 25),
 (5, 27);
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_users (19 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_users ON;
INSERT INTO dbo.tbl_users ([id], [username], [password_hash], [full_name], [phone], [email], [is_active], [security_stamp], [failed_login_count], [locked_until], [last_login_at], [created_at], [updated_at]) VALUES
 (1, 'admin', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Nguyễn Văn Quản Trị', '0901000001', 'admin@gridsmart.local', 1, '206ea9f8-58db-424e-a676-e0d640c6ec83', 0, NULL, '2026-10-11T07:48:43', '2026-10-10T03:00:50', NULL),
 (2, 'leader.tinh', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Trịnh Văn Lãnh Đạo Tỉnh', '0901000004', 'leader.tinh@gridsmart.local', 1, '376d7aa9-30df-4cc9-aee0-b7affaeae287', 0, NULL, '2026-10-11T09:07:36', '2026-10-10T03:00:50', NULL),
 (3, 'dispatcher01', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Trần Thị Điều Phối', '0901000002', 'dispatcher@gridsmart.local', 1, '2db0b2a8-c0ca-4136-bf85-c0b385a875b7', 0, NULL, '2026-10-11T08:17:34', '2026-10-10T03:00:50', NULL),
 (4, 'leader.tb', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Lê Văn Lãnh Đạo TB', '0901000003', 'leader.tb@gridsmart.local', 1, '271cebf4-753e-46d2-9bcd-50d5b501ac25', 0, NULL, '2026-10-11T08:17:34', '2026-10-10T03:00:50', NULL),
 (5, 'leader.bh', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Mai Thị Lãnh Đạo BH', '0901000005', 'leader.bh@gridsmart.local', 1, 'ccacf2ac-982e-4757-ac20-589e6d743b39', 0, NULL, '2026-10-11T08:17:34', '2026-10-10T03:00:50', NULL),
 (6, 'leader.th', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Đỗ Văn Lãnh Đạo TH', '0901000006', 'leader.th@gridsmart.local', 1, 'de81820a-074f-405f-bcdc-f8a119573ed9', 0, NULL, NULL, '2026-10-10T03:00:50', NULL),
 (7, 'officer01', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Phạm Minh Cán Bộ 01', '0902000001', NULL, 1, '05903ee7-5f69-43a7-aee8-2583a1a552df', 0, NULL, '2026-10-10T08:33:19', '2026-10-10T03:00:50', NULL),
 (8, 'officer02', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Hoàng Thị Cán Bộ 02', '0902000002', NULL, 1, '7deacae8-f9bb-46b2-b4c5-b793a7ceefbc', 0, NULL, '2026-10-11T09:11:55', '2026-10-10T03:00:50', NULL),
 (9, 'officer03', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Võ Văn Cán Bộ 03', '0902000003', NULL, 1, '7c34a0d7-934c-44dc-aac5-e059cb4f1544', 0, '9999-12-31T00:00:00', NULL, '2026-10-10T03:00:50', '2026-10-10T08:42:35'),
 (10, 'officer04', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Đặng Thị Cán Bộ 04', '0902000004', NULL, 1, '9979478c-82e0-48d9-9362-3d293176a736', 0, NULL, NULL, '2026-10-10T03:00:50', NULL),
 (11, 'officer05', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Bùi Văn Cán Bộ 05', '0902000005', NULL, 1, '90e3587d-3aae-4b2b-a422-4ab1bcc60f09', 0, NULL, NULL, '2026-10-10T03:00:50', NULL),
 (12, 'officer06', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Ngô Thị Cán Bộ 06', '0902000006', NULL, 1, '129d2f3e-daba-4868-a7fd-a93019ecaee2', 0, NULL, NULL, '2026-10-10T03:00:50', '2026-10-10T09:17:58'),
 (13, 'citizen01', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Công dân mẫu', '0903000001', NULL, 1, '26d80ce7-171f-4834-8308-ff79dcf2db2b', 0, NULL, '2026-10-10T08:33:18', '2026-10-10T03:00:50', NULL),
 (15, 'officer07', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Trương Văn Cán Bộ 07', '0902000007', NULL, 1, 'd89d71c7-fdb1-4e23-a0ea-5cf7a3e0dfb2', 0, NULL, '2026-10-11T08:17:34', '2026-10-11T08:10:16', NULL),
 (16, 'officer08', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Lý Thị Cán Bộ 08', '0902000008', NULL, 1, 'a72d55fc-5163-4da0-a551-a7d37ae06a79', 0, NULL, NULL, '2026-10-11T08:10:16', NULL),
 (17, 'officer09', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Dương Văn Cán Bộ 09', '0902000009', NULL, 1, '3a6985dd-b715-430b-b39b-5141511b839f', 0, NULL, NULL, '2026-10-11T08:10:16', NULL),
 (18, 'officer10', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Phan Thị Cán Bộ 10', '0902000010', NULL, 1, '1a2bbdee-7913-4c50-b33c-f21318c95cd8', 0, NULL, NULL, '2026-10-11T08:10:16', NULL),
 (19, 'officer11', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Huỳnh Văn Cán Bộ 11', '0902000011', NULL, 1, '3c6e0f88-6aa2-4253-8f1a-786c38b612c9', 0, NULL, NULL, '2026-10-11T08:10:16', NULL),
 (20, 'officer12', 'AQAAAAIAAYagAAAAELmUDRiEqbs7p2e0SpuTlbaVP4Qx7Q9J+K9TWJHJZ467mseDHETs7Ivgul45EGP7UQ==', N'Lâm Thị Cán Bộ 12', '0902000012', NULL, 1, 'b06bfde1-cb28-475e-89b6-734b2cb656b3', 0, NULL, '2026-10-11T08:17:34', '2026-10-11T08:10:16', NULL);
SET IDENTITY_INSERT dbo.tbl_users OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_user_roles (46 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_user_roles ON;
INSERT INTO dbo.tbl_user_roles ([id], [user_id], [role_id], [admin_unit_id], [assigned_by_user_id], [assigned_at], [expires_at], [is_active]) VALUES
 (1, 1, 5, NULL, 1, '2026-10-10T03:00:50', NULL, 1),
 (14, 13, 1, NULL, 1, '2026-10-10T03:00:50', NULL, 1),
 (16, 9, 3, NULL, 1, '2026-10-10T08:42:02', NULL, 0),
 (103, 2, 4, 1, 1, '2026-10-11T08:12:01', NULL, 1),
 (104, 3, 3, 1, 1, '2026-10-11T08:12:01', NULL, 1),
 (105, 4, 4, 7, 1, '2026-10-11T08:12:01', NULL, 1),
 (106, 4, 3, 7, 1, '2026-10-11T08:12:01', NULL, 1),
 (107, 5, 4, 2, 1, '2026-10-11T08:12:01', NULL, 1),
 (108, 6, 4, 11, 1, '2026-10-11T08:12:01', NULL, 1),
 (109, 9, 2, 12, 1, '2026-10-11T08:12:01', NULL, 1),
 (110, 9, 2, 13, 1, '2026-10-11T08:12:01', NULL, 1),
 (111, 9, 2, 14, 1, '2026-10-11T08:12:01', NULL, 1),
 (112, 9, 2, 15, 1, '2026-10-11T08:12:01', NULL, 1),
 (113, 10, 2, 16, 1, '2026-10-11T08:12:01', NULL, 1),
 (114, 10, 2, 17, 1, '2026-10-11T08:12:01', NULL, 1),
 (115, 10, 2, 18, 1, '2026-10-11T08:12:01', NULL, 1),
 (116, 18, 2, 19, 1, '2026-10-11T08:12:01', NULL, 1),
 (117, 18, 2, 20, 1, '2026-10-11T08:12:01', NULL, 1),
 (118, 18, 2, 21, 1, '2026-10-11T08:12:01', NULL, 1),
 (119, 18, 2, 22, 1, '2026-10-11T08:12:01', NULL, 1),
 (120, 19, 2, 23, 1, '2026-10-11T08:12:01', NULL, 1),
 (121, 19, 2, 24, 1, '2026-10-11T08:12:01', NULL, 1),
 (122, 19, 2, 25, 1, '2026-10-11T08:12:01', NULL, 1),
 (123, 7, 2, 26, 1, '2026-10-11T08:12:01', NULL, 1),
 (124, 7, 2, 27, 1, '2026-10-11T08:12:01', NULL, 1),
 (125, 7, 2, 28, 1, '2026-10-11T08:12:01', NULL, 1),
 (126, 8, 2, 29, 1, '2026-10-11T08:12:01', NULL, 1),
 (127, 8, 2, 30, 1, '2026-10-11T08:12:01', NULL, 1),
 (128, 8, 2, 31, 1, '2026-10-11T08:12:01', NULL, 1),
 (129, 15, 2, 32, 1, '2026-10-11T08:12:01', NULL, 1),
 (130, 15, 2, 33, 1, '2026-10-11T08:12:01', NULL, 1),
 (131, 15, 2, 34, 1, '2026-10-11T08:12:01', NULL, 1),
 (132, 16, 2, 35, 1, '2026-10-11T08:12:01', NULL, 1),
 (133, 16, 2, 36, 1, '2026-10-11T08:12:01', NULL, 1),
 (134, 16, 2, 37, 1, '2026-10-11T08:12:01', NULL, 1),
 (135, 17, 2, 38, 1, '2026-10-11T08:12:01', NULL, 1),
 (136, 17, 2, 39, 1, '2026-10-11T08:12:01', NULL, 1),
 (137, 17, 2, 40, 1, '2026-10-11T08:12:01', NULL, 1),
 (138, 11, 2, 41, 1, '2026-10-11T08:12:01', NULL, 1),
 (139, 11, 2, 42, 1, '2026-10-11T08:12:01', NULL, 1),
 (140, 11, 2, 43, 1, '2026-10-11T08:12:01', NULL, 1),
 (141, 12, 2, 44, 1, '2026-10-11T08:12:01', NULL, 1),
 (142, 12, 2, 45, 1, '2026-10-11T08:12:01', NULL, 1),
 (143, 12, 2, 46, 1, '2026-10-11T08:12:01', NULL, 1),
 (144, 20, 2, 47, 1, '2026-10-11T08:12:01', NULL, 1),
 (145, 20, 2, 48, 1, '2026-10-11T08:12:01', NULL, 1);
SET IDENTITY_INSERT dbo.tbl_user_roles OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_categories (5 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_categories ON;
INSERT INTO dbo.tbl_categories ([id], [category_code], [category_name], [default_priority], [is_active]) VALUES
 (1, 'WASTE', N'Rác thải', 2, 1),
 (2, 'ENCROACHMENT', N'Lấn chiếm vỉa hè', 1, 1),
 (3, 'INFRASTRUCTURE', N'Hư hỏng hạ tầng', 3, 1),
 (4, 'ILLEGAL_DUMP', N'Xả thải', 3, 1),
 (6, 'TEST', N'ohh test ne', 2, 1);
SET IDENTITY_INSERT dbo.tbl_categories OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_sla_policies (3 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_sla_policies ON;
INSERT INTO dbo.tbl_sla_policies ([id], [priority], [sla_level], [sla_hours], [escalation_percent], [description]) VALUES
 (1, 3, 1, 4, 80, N'Khẩn cấp - Level 1: 4 giờ'),
 (2, 2, 2, 24, 80, N'Trung bình - Level 2: 24 giờ'),
 (3, 1, 3, 48, 80, N'Thấp - Level 3: 48 giờ');
SET IDENTITY_INSERT dbo.tbl_sla_policies OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_grids (54 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_grids ON;
INSERT INTO dbo.tbl_grids ([id], [grid_code], [name], [admin_unit_id], [boundary], [assigned_officer_id], [is_active], [created_at], [updated_at]) VALUES
 (1, 'DNI-BH-KP01-G01', N'Khu phố 1, Phường Biên Hòa', 12, geography::STGeomFromText('POLYGON ((106.77720000000001 10.944, 106.788 10.944, 106.788 10.9524, 106.775 10.955, 106.77720000000001 10.944))', 4326), 9, 1, '2026-10-11T08:12:02', NULL),
 (2, 'DNI-BH-KP02-G01', N'Khu phố 2, Phường Biên Hòa', 13, geography::STGeomFromText('POLYGON ((106.788 10.944, 106.8 10.944, 106.8 10.95, 106.788 10.9524, 106.788 10.944))', 4326), 9, 1, '2026-10-11T08:12:02', NULL),
 (3, 'DNI-BH-KP03-G01', N'Khu phố 3, Phường Biên Hòa', 14, geography::STGeomFromText('POLYGON ((106.7796 10.932, 106.788 10.932, 106.788 10.944, 106.77720000000001 10.944, 106.7796 10.932))', 4326), 9, 1, '2026-10-11T08:12:02', NULL),
 (4, 'DNI-BH-KP04-G01', N'Khu phố 4 (ô 1), Phường Biên Hòa', 15, geography::STGeomFromText('POLYGON ((106.788 10.938, 106.8 10.938, 106.8 10.944, 106.788 10.944, 106.788 10.938))', 4326), 9, 1, '2026-10-11T08:12:02', NULL),
 (5, 'DNI-BH-KP04-G02', N'Khu phố 4 (ô 2), Phường Biên Hòa', 15, geography::STGeomFromText('POLYGON ((106.788 10.932, 106.8 10.932, 106.8 10.938, 106.788 10.938, 106.788 10.932))', 4326), 9, 1, '2026-10-11T08:12:02', NULL),
 (6, 'DNI-BH-KP05-G01', N'Khu phố 5, Phường Biên Hòa', 16, geography::STGeomFromText('POLYGON ((106.8 10.932, 106.812 10.932, 106.81733333333332 10.932, 106.812 10.94, 106.8 10.95, 106.8 10.944, 106.8 10.932))', 4326), 10, 1, '2026-10-11T08:12:02', NULL),
 (7, 'DNI-BH-KP06-G01', N'Khu phố 6 (ô 1), Phường Biên Hòa', 17, geography::STGeomFromText('POLYGON ((106.78833333333334 10.92, 106.7898 10.92, 106.7898 10.932, 106.788 10.932, 106.7796 10.932, 106.78 10.93, 106.78833333333334 10.92))', 4326), 10, 1, '2026-10-11T08:12:02', NULL),
 (8, 'DNI-BH-KP06-G02', N'Khu phố 6 (ô 2), Phường Biên Hòa', 17, geography::STGeomFromText('POLYGON ((106.7898 10.92, 106.8 10.92, 106.8 10.932, 106.7898 10.932, 106.7898 10.92))', 4326), 10, 1, '2026-10-11T08:12:02', NULL),
 (9, 'DNI-BH-KP07-G01', N'Khu phố 7 (ô 1), Phường Biên Hòa', 18, geography::STGeomFromText('POLYGON ((106.8 10.926, 106.812 10.926, 106.812 10.932, 106.8 10.932, 106.8 10.926))', 4326), 10, 1, '2026-10-11T08:12:02', NULL),
 (10, 'DNI-BH-KP07-G02', N'Khu phố 7 (ô 2), Phường Biên Hòa', 18, geography::STGeomFromText('POLYGON ((106.8 10.92, 106.812 10.92, 106.812 10.926, 106.8 10.926, 106.8 10.92))', 4326), 10, 1, '2026-10-11T08:12:02', NULL),
 (11, 'DNI-BH-KP08-G01', N'Khu phố 8, Phường Biên Hòa', 19, geography::STGeomFromText('POLYGON ((106.812 10.92, 106.824 10.92, 106.8273846153846 10.92, 106.82 10.928, 106.81733333333332 10.932, 106.812 10.932, 106.812 10.92))', 4326), 18, 1, '2026-10-11T08:12:02', NULL),
 (12, 'DNI-BH-KP09-G01', N'Khu phố 9, Phường Biên Hòa', 20, geography::STGeomFromText('POLYGON ((106.8 10.908500000000004, 106.8 10.92, 106.78833333333334 10.92, 106.795 10.912, 106.8 10.908500000000004))', 4326), 18, 1, '2026-10-11T08:12:02', NULL),
 (13, 'DNI-BH-KP10-G01', N'Khu phố 10 (ô 1), Phường Biên Hòa', 21, geography::STGeomFromText('POLYGON ((106.8 10.91005, 106.812 10.91005, 106.812 10.92, 106.8 10.92, 106.8 10.91005))', 4326), 18, 1, '2026-10-11T08:12:02', NULL),
 (14, 'DNI-BH-KP10-G02', N'Khu phố 10 (ô 2), Phường Biên Hòa', 21, geography::STGeomFromText('POLYGON ((106.812 10.9001, 106.812 10.908, 106.812 10.91005, 106.8 10.91005, 106.8 10.908500000000004, 106.812 10.9001))', 4326), 18, 1, '2026-10-11T08:12:02', NULL),
 (15, 'DNI-BH-KP11-G01', N'Khu phố 11 (ô 1), Phường Biên Hòa', 22, geography::STGeomFromText('POLYGON ((106.812 10.914, 106.824 10.914, 106.824 10.92, 106.812 10.92, 106.812 10.914))', 4326), 18, 1, '2026-10-11T08:12:02', NULL),
 (16, 'DNI-BH-KP11-G02', N'Khu phố 11 (ô 2), Phường Biên Hòa', 22, geography::STGeomFromText('POLYGON ((106.812 10.908, 106.824 10.908, 106.824 10.914, 106.812 10.914, 106.812 10.908))', 4326), 18, 1, '2026-10-11T08:12:02', NULL),
 (17, 'DNI-BH-KP12-G01', N'Khu phố 12, Phường Biên Hòa', 23, geography::STGeomFromText('POLYGON ((106.824 10.908, 106.8334 10.908, 106.832 10.915, 106.8273846153846 10.92, 106.824 10.92, 106.824 10.908))', 4326), 19, 1, '2026-10-11T08:12:02', NULL),
 (18, 'DNI-BH-KP13-G01', N'Khu phố 13, Phường Biên Hòa', 24, geography::STGeomFromText('POLYGON ((106.815 10.898, 106.824 10.898900000000001, 106.824 10.908, 106.812 10.908, 106.812 10.9001, 106.815 10.898))', 4326), 19, 1, '2026-10-11T08:12:02', NULL),
 (19, 'DNI-BH-KP14-G01', N'Khu phố 14, Phường Biên Hòa', 25, geography::STGeomFromText('POLYGON ((106.824 10.898900000000001, 106.835 10.9, 106.8334 10.908, 106.824 10.908, 106.824 10.898900000000001))', 4326), 19, 1, '2026-10-11T08:12:02', NULL),
 (20, 'DNI-TB-KP01-G01', N'Khu phố 1, Phường Trấn Biên', 26, geography::STGeomFromText('POLYGON ((106.79727272727273 10.956, 106.8 10.956, 106.8 10.968, 106.8 10.9704, 106.79 10.972, 106.79727272727273 10.956))', 4326), 7, 1, '2026-10-11T08:12:02', NULL),
 (21, 'DNI-TB-KP02-G01', N'Khu phố 2 (ô 1), Phường Trấn Biên', 27, geography::STGeomFromText('POLYGON ((106.8 10.9632, 106.812 10.9632, 106.812 10.968, 106.812 10.96848, 106.8 10.9704, 106.8 10.968, 106.8 10.9632))', 4326), 7, 1, '2026-10-11T08:12:02', NULL),
 (22, 'DNI-TB-KP02-G02', N'Khu phố 2 (ô 2), Phường Trấn Biên', 27, geography::STGeomFromText('POLYGON ((106.8 10.956, 106.812 10.956, 106.812 10.9632, 106.8 10.9632, 106.8 10.956))', 4326), 7, 1, '2026-10-11T08:12:02', NULL),
 (23, 'DNI-TB-KP03-G01', N'Khu phố 3 (ô 1), Phường Trấn Biên', 28, geography::STGeomFromText('POLYGON ((106.812 10.96224, 106.824 10.96224, 106.824 10.964823529411763, 106.815 10.968, 106.812 10.96848, 106.812 10.968, 106.812 10.96224))', 4326), 7, 1, '2026-10-11T08:12:02', NULL),
 (24, 'DNI-TB-KP03-G02', N'Khu phố 3 (ô 2), Phường Trấn Biên', 28, geography::STGeomFromText('POLYGON ((106.812 10.956, 106.824 10.956, 106.824 10.96224, 106.812 10.96224, 106.812 10.956))', 4326), 7, 1, '2026-10-11T08:12:02', NULL),
 (25, 'DNI-TB-KP04-G01', N'Khu phố 4, Phường Trấn Biên', 29, geography::STGeomFromText('POLYGON ((106.824 10.956, 106.8356 10.956, 106.832 10.962, 106.824 10.964823529411763, 106.824 10.956))', 4326), 8, 1, '2026-10-11T08:12:02', NULL),
 (26, 'DNI-TB-KP05-G01', N'Khu phố 5 (ô 1), Phường Trấn Biên', 30, geography::STGeomFromText('POLYGON ((106.804636 10.946136666666661, 106.804636 10.956, 106.8 10.956, 106.79727272727273 10.956, 106.8 10.95, 106.804636 10.946136666666661))', 4326), 8, 1, '2026-10-11T08:12:02', NULL),
 (27, 'DNI-TB-KP05-G02', N'Khu phố 5 (ô 2), Phường Trấn Biên', 30, geography::STGeomFromText('POLYGON ((106.8072 10.944, 106.812 10.944, 106.812 10.956, 106.804636 10.956, 106.804636 10.946136666666661, 106.8072 10.944))', 4326), 8, 1, '2026-10-11T08:12:02', NULL),
 (28, 'DNI-TB-KP06-G01', N'Khu phố 6 (ô 1), Phường Trấn Biên', 31, geography::STGeomFromText('POLYGON ((106.812 10.95, 106.824 10.95, 106.824 10.956, 106.812 10.956, 106.812 10.95))', 4326), 8, 1, '2026-10-11T08:12:02', NULL),
 (29, 'DNI-TB-KP06-G02', N'Khu phố 6 (ô 2), Phường Trấn Biên', 31, geography::STGeomFromText('POLYGON ((106.812 10.944, 106.824 10.944, 106.824 10.95, 106.812 10.95, 106.812 10.944))', 4326), 8, 1, '2026-10-11T08:12:02', NULL),
 (30, 'DNI-TB-KP07-G01', N'Khu phố 7 (ô 1), Phường Trấn Biên', 32, geography::STGeomFromText('POLYGON ((106.824 10.944, 106.833333 10.944, 106.833333 10.956, 106.824 10.956, 106.824 10.944))', 4326), 15, 1, '2026-10-11T08:12:02', NULL),
 (31, 'DNI-TB-KP07-G02', N'Khu phố 7 (ô 2), Phường Trấn Biên', 32, geography::STGeomFromText('POLYGON ((106.833333 10.944, 106.836 10.944, 106.84266666666666 10.944, 106.838 10.952, 106.8356 10.956, 106.833333 10.956, 106.833333 10.944))', 4326), 15, 1, '2026-10-11T08:12:02', NULL),
 (32, 'DNI-TB-KP08-G01', N'Khu phố 8, Phường Trấn Biên', 33, geography::STGeomFromText('POLYGON ((106.81733333333332 10.932, 106.824 10.932, 106.824 10.944, 106.812 10.944, 106.8072 10.944, 106.812 10.94, 106.81733333333332 10.932))', 4326), 15, 1, '2026-10-11T08:12:02', NULL),
 (33, 'DNI-TB-KP09-G01', N'Khu phố 9 (ô 1), Phường Trấn Biên', 34, geography::STGeomFromText('POLYGON ((106.824 10.938, 106.836 10.938, 106.836 10.944, 106.824 10.944, 106.824 10.938))', 4326), 15, 1, '2026-10-11T08:12:02', NULL),
 (34, 'DNI-TB-KP09-G02', N'Khu phố 9 (ô 2), Phường Trấn Biên', 34, geography::STGeomFromText('POLYGON ((106.824 10.932, 106.836 10.932, 106.836 10.938, 106.824 10.938, 106.824 10.932))', 4326), 15, 1, '2026-10-11T08:12:02', NULL),
 (35, 'DNI-TB-KP10-G01', N'Khu phố 10, Phường Trấn Biên', 35, geography::STGeomFromText('POLYGON ((106.836 10.932, 106.848 10.932, 106.85300000000001 10.932, 106.845 10.94, 106.84266666666666 10.944, 106.836 10.944, 106.836 10.932))', 4326), 16, 1, '2026-10-11T08:12:02', NULL),
 (36, 'DNI-TB-KP11-G01', N'Khu phố 11, Phường Trấn Biên', 36, geography::STGeomFromText('POLYGON ((106.836 10.911923076923074, 106.836 10.92, 106.836 10.932, 106.824 10.932, 106.81733333333332 10.932, 106.82 10.928, 106.832 10.915, 106.836 10.911923076923074))', 4326), 16, 1, '2026-10-11T08:12:02', NULL),
 (37, 'DNI-TB-KP12-G01', N'Khu phố 12 (ô 1), Phường Trấn Biên', 37, geography::STGeomFromText('POLYGON ((106.836 10.926, 106.848 10.926, 106.848 10.932, 106.836 10.932, 106.836 10.926))', 4326), 16, 1, '2026-10-11T08:12:02', NULL),
 (38, 'DNI-TB-KP12-G02', N'Khu phố 12 (ô 2), Phường Trấn Biên', 37, geography::STGeomFromText('POLYGON ((106.836 10.92, 106.848 10.92, 106.848 10.926, 106.836 10.926, 106.836 10.92))', 4326), 16, 1, '2026-10-11T08:12:02', NULL),
 (39, 'DNI-TB-KP13-G01', N'Khu phố 13, Phường Trấn Biên', 38, geography::STGeomFromText('POLYGON ((106.848 10.92, 106.85966666666667 10.92, 106.855 10.93, 106.85300000000001 10.932, 106.848 10.932, 106.848 10.92))', 4326), 17, 1, '2026-10-11T08:12:02', NULL),
 (40, 'DNI-TB-KP14-G01', N'Khu phố 14 (ô 1), Phường Trấn Biên', 39, geography::STGeomFromText('POLYGON ((106.836 10.9125, 106.848 10.9125, 106.848 10.92, 106.836 10.92, 106.836 10.9125))', 4326), 17, 1, '2026-10-11T08:12:02', NULL),
 (41, 'DNI-TB-KP14-G02', N'Khu phố 14 (ô 2), Phường Trấn Biên', 39, geography::STGeomFromText('POLYGON ((106.845 10.905, 106.848 10.906764705882352, 106.848 10.908, 106.848 10.9125, 106.836 10.9125, 106.836 10.911923076923074, 106.845 10.905))', 4326), 17, 1, '2026-10-11T08:12:02', NULL),
 (42, 'DNI-TB-KP15-G01', N'Khu phố 15, Phường Trấn Biên', 40, geography::STGeomFromText('POLYGON ((106.848 10.906764705882352, 106.862 10.915, 106.85966666666667 10.92, 106.848 10.92, 106.848 10.908, 106.848 10.906764705882352))', 4326), 17, 1, '2026-10-11T08:12:02', NULL),
 (43, 'DNI-TH-KP01-G01', N'Khu phố 1 (ô 1), Phường Tam Hiệp', 41, geography::STGeomFromText('POLYGON ((106.8356 10.956, 106.836 10.956, 106.84 10.956, 106.84 10.965692307692311, 106.832 10.962, 106.8356 10.956))', 4326), 11, 1, '2026-10-11T08:12:02', NULL),
 (44, 'DNI-TH-KP01-G02', N'Khu phố 1 (ô 2), Phường Tam Hiệp', 41, geography::STGeomFromText('POLYGON ((106.84 10.956, 106.848 10.956, 106.848 10.968, 106.845 10.968, 106.84 10.965692307692311, 106.84 10.956))', 4326), 11, 1, '2026-10-11T08:12:02', NULL),
 (45, 'DNI-TH-KP02-G01', N'Khu phố 2 (ô 1), Phường Tam Hiệp', 42, geography::STGeomFromText('POLYGON ((106.848 10.956, 106.856667 10.956, 106.856667 10.968, 106.848 10.968, 106.848 10.956))', 4326), 11, 1, '2026-10-11T08:12:02', NULL),
 (46, 'DNI-TH-KP02-G02', N'Khu phố 2 (ô 2), Phường Tam Hiệp', 42, geography::STGeomFromText('POLYGON ((106.856667 10.956, 106.86 10.956, 106.86533333333333 10.956, 106.86 10.968, 106.856667 10.968, 106.856667 10.956))', 4326), 11, 1, '2026-10-11T08:12:02', NULL),
 (47, 'DNI-TH-KP03-G01', N'Khu phố 3, Phường Tam Hiệp', 43, geography::STGeomFromText('POLYGON ((106.84266666666666 10.944, 106.848 10.944, 106.848 10.956, 106.836 10.956, 106.836 10.955333333333325, 106.838 10.952, 106.84266666666666 10.944))', 4326), 11, 1, '2026-10-11T08:12:02', NULL),
 (48, 'DNI-TH-KP04-G01', N'Khu phố 4 (ô 1), Phường Tam Hiệp', 44, geography::STGeomFromText('POLYGON ((106.848 10.95, 106.86 10.95, 106.86 10.956, 106.848 10.956, 106.848 10.95))', 4326), 12, 1, '2026-10-11T08:12:02', NULL),
 (49, 'DNI-TH-KP04-G02', N'Khu phố 4 (ô 2), Phường Tam Hiệp', 44, geography::STGeomFromText('POLYGON ((106.848 10.944, 106.86 10.944, 106.86 10.95, 106.848 10.95, 106.848 10.944))', 4326), 12, 1, '2026-10-11T08:12:02', NULL),
 (50, 'DNI-TH-KP05-G01', N'Khu phố 5, Phường Tam Hiệp', 45, geography::STGeomFromText('POLYGON ((106.86 10.944, 106.8690909090909 10.944, 106.868 10.95, 106.86533333333333 10.956, 106.86 10.956, 106.86 10.944))', 4326), 12, 1, '2026-10-11T08:12:02', NULL),
 (51, 'DNI-TH-KP06-G01', N'Khu phố 6, Phường Tam Hiệp', 46, geography::STGeomFromText('POLYGON ((106.85966666666667 10.92, 106.86 10.92, 106.86 10.932, 106.86 10.944, 106.848 10.944, 106.84266666666666 10.944, 106.845 10.94, 106.855 10.93, 106.85966666666667 10.92))', 4326), 12, 1, '2026-10-11T08:12:02', NULL),
 (52, 'DNI-TH-KP07-G01', N'Khu phố 7, Phường Tam Hiệp', 47, geography::STGeomFromText('POLYGON ((106.86 10.932, 106.87127272727272 10.932, 106.8690909090909 10.944, 106.86 10.944, 106.86 10.932))', 4326), 20, 1, '2026-10-11T08:12:02', NULL),
 (53, 'DNI-TH-KP08-G01', N'Khu phố 8 (ô 1), Phường Tam Hiệp', 48, geography::STGeomFromText('POLYGON ((106.86 10.9235, 106.86853846153846 10.9235, 106.872 10.928, 106.87127272727272 10.932, 106.86 10.932, 106.86 10.9235))', 4326), 20, 1, '2026-10-11T08:12:02', NULL),
 (54, 'DNI-TH-KP08-G02', N'Khu phố 8 (ô 2), Phường Tam Hiệp', 48, geography::STGeomFromText('POLYGON ((106.862 10.915, 106.86853846153846 10.9235, 106.86 10.9235, 106.86 10.92, 106.86 10.91928571428571, 106.862 10.915))', 4326), 20, 1, '2026-10-11T08:12:02', NULL);
SET IDENTITY_INSERT dbo.tbl_grids OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_grid_officers (54 dong) ---------- */
INSERT INTO dbo.tbl_grid_officers ([grid_id], [user_id], [is_primary], [assigned_at]) VALUES
 (1, 9, 1, '2026-10-11T08:12:02'),
 (2, 9, 1, '2026-10-11T08:12:02'),
 (3, 9, 1, '2026-10-11T08:12:02'),
 (4, 9, 1, '2026-10-11T08:12:02'),
 (5, 9, 1, '2026-10-11T08:12:02'),
 (6, 10, 1, '2026-10-11T08:12:02'),
 (7, 10, 1, '2026-10-11T08:12:02'),
 (8, 10, 1, '2026-10-11T08:12:02'),
 (9, 10, 1, '2026-10-11T08:12:02'),
 (10, 10, 1, '2026-10-11T08:12:02'),
 (11, 18, 1, '2026-10-11T08:12:02'),
 (12, 18, 1, '2026-10-11T08:12:02'),
 (13, 18, 1, '2026-10-11T08:12:02'),
 (14, 18, 1, '2026-10-11T08:12:02'),
 (15, 18, 1, '2026-10-11T08:12:02'),
 (16, 18, 1, '2026-10-11T08:12:02'),
 (17, 19, 1, '2026-10-11T08:12:02'),
 (18, 19, 1, '2026-10-11T08:12:02'),
 (19, 19, 1, '2026-10-11T08:12:02'),
 (20, 7, 1, '2026-10-11T08:12:02'),
 (21, 7, 1, '2026-10-11T08:12:02'),
 (22, 7, 1, '2026-10-11T08:12:02'),
 (23, 7, 1, '2026-10-11T08:12:02'),
 (24, 7, 1, '2026-10-11T08:12:02'),
 (25, 8, 1, '2026-10-11T08:12:02'),
 (26, 8, 1, '2026-10-11T08:12:02'),
 (27, 8, 1, '2026-10-11T08:12:02'),
 (28, 8, 1, '2026-10-11T08:12:02'),
 (29, 8, 1, '2026-10-11T08:12:02'),
 (30, 15, 1, '2026-10-11T08:12:02'),
 (31, 15, 1, '2026-10-11T08:12:02'),
 (32, 15, 1, '2026-10-11T08:12:02'),
 (33, 15, 1, '2026-10-11T08:12:02'),
 (34, 15, 1, '2026-10-11T08:12:02'),
 (35, 16, 1, '2026-10-11T08:12:02'),
 (36, 16, 1, '2026-10-11T08:12:02'),
 (37, 16, 1, '2026-10-11T08:12:02'),
 (38, 16, 1, '2026-10-11T08:12:02'),
 (39, 17, 1, '2026-10-11T08:12:02'),
 (40, 17, 1, '2026-10-11T08:12:02'),
 (41, 17, 1, '2026-10-11T08:12:02'),
 (42, 17, 1, '2026-10-11T08:12:02'),
 (43, 11, 1, '2026-10-11T08:12:02'),
 (44, 11, 1, '2026-10-11T08:12:02'),
 (45, 11, 1, '2026-10-11T08:12:02'),
 (46, 11, 1, '2026-10-11T08:12:02'),
 (47, 11, 1, '2026-10-11T08:12:02'),
 (48, 12, 1, '2026-10-11T08:12:02'),
 (49, 12, 1, '2026-10-11T08:12:02'),
 (50, 12, 1, '2026-10-11T08:12:02'),
 (51, 12, 1, '2026-10-11T08:12:02'),
 (52, 20, 1, '2026-10-11T08:12:02'),
 (53, 20, 1, '2026-10-11T08:12:02'),
 (54, 20, 1, '2026-10-11T08:12:02');
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_incidents (40 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_incidents ON;
INSERT INTO dbo.tbl_incidents ([id], [tracking_code], [reporter_user_id], [reporter_phone], [description], [category_id], [ai_label], [ai_confidence], [is_manual_review], [location_point], [address_text], [grid_id], [admin_unit_id], [assigned_officer_id], [parent_incident_id], [duplicate_count], [status], [priority], [sla_policy_id], [sla_deadline], [accepted_at], [is_overdue], [escalated_at], [resolved_at], [created_at], [updated_at], [processed_at]) VALUES
 (1, 'DN-2026-K7QM', 13, '0903000001', N'Rác thải sinh hoạt dồn ứ ở góc chợ Biên Hòa, bốc mùi khó chịu.', 1, N'waste', 0.9312, 0, geography::STGeomFromText('POINT (106.8195 10.9475)', 4326), N'Đường Cách Mạng Tháng Tám, gần chợ Biên Hòa, phường Trấn Biên', 29, 31, 8, NULL, 0, 3, 2, 2, '2026-09-15T06:14:00', '2026-09-14T06:39:00', 0, NULL, '2026-09-14T12:39:00', '2026-09-14T06:12:00', '2026-09-14T12:39:00', '2026-09-14T06:12:10'),
 (2, 'DN-2026-P3XT', NULL, '0918234561', N'Ổ gà lớn trên Quốc lộ 1K, xe máy dễ té ngã khi trời mưa.', 3, N'infrastructure', 0.8870, 0, geography::STGeomFromText('POINT (106.795 10.94)', 4326), N'Quốc lộ 1K, đoạn qua khu vực Hóa An, phường Biên Hòa', 4, 15, 9, NULL, 0, 3, 3, 1, '2026-09-16T12:14:00', '2026-09-16T08:54:00', 0, NULL, '2026-09-16T12:06:00', '2026-09-16T08:12:00', '2026-09-16T12:06:00', '2026-09-16T08:12:10'),
 (3, 'DN-2026-R8VD', NULL, '0909876123', N'Quán nước dựng bàn ghế chiếm hết vỉa hè, người đi bộ phải xuống lòng đường.', 2, N'encroachment', 0.9105, 0, geography::STGeomFromText('POINT (106.845 10.955)', 4326), N'Đường Phạm Văn Thuận, khu vực Tân Mai, phường Tam Hiệp', 47, 43, 11, NULL, 0, 3, 1, 3, '2026-09-20T20:14:00', '2026-09-18T23:14:00', 0, NULL, '2026-09-19T19:14:00', '2026-09-18T20:12:00', '2026-09-19T19:14:00', '2026-09-18T20:12:10'),
 (4, 'DN-2026-H2NB', 13, '0903000001', N'Cơ sở sát bờ sông xả nước thải màu đen ra sông Đồng Nai, mùi hôi nồng.', 4, N'illegal_dump', 0.8640, 0, geography::STGeomFromText('POINT (106.833 10.933)', 4326), N'Khu vực Cù Lao Phố (Hiệp Hòa cũ), phường Trấn Biên', 34, 34, 15, NULL, 0, 3, 3, 1, '2026-09-21T12:14:00', '2026-09-21T08:44:00', 1, NULL, '2026-09-21T14:14:00', '2026-09-21T08:12:00', '2026-09-21T14:14:00', '2026-09-21T08:12:10'),
 (5, 'DN-2026-W5CK', NULL, '0977112233', N'Bãi rác tự phát dưới chân cầu, nhiều túi rác và xà bần.', 1, N'waste', 0.9020, 0, geography::STGeomFromText('POINT (106.823 10.91)', 4326), N'Đường Bùi Hữu Nghĩa, khu vực Tân Vạn, phường Biên Hòa', 16, 22, 18, NULL, 0, 3, 2, 2, '2026-09-24T10:14:00', '2026-09-23T11:14:00', 0, NULL, '2026-09-24T05:14:00', '2026-09-23T10:12:00', '2026-09-24T05:14:00', '2026-09-23T10:12:10'),
 (6, 'DN-2026-T9LF', NULL, '0933445566', N'Đèn đường không sáng cả đoạn gần vòng xoay Tam Hiệp, buổi tối rất tối.', 3, N'infrastructure', 0.9380, 0, geography::STGeomFromText('POINT (106.864 10.929)', 4326), N'Gần vòng xoay Tam Hiệp, phường Tam Hiệp', 53, 48, 20, NULL, 1, 3, 3, 1, '2026-09-25T16:14:00', '2026-09-25T12:29:00', 0, NULL, '2026-09-25T14:59:00', '2026-09-25T12:12:00', '2026-09-25T14:59:00', '2026-09-25T12:12:10'),
 (7, 'DN-2026-M4GZ', NULL, '0965321789', N'Xe tải đậu chiếm lòng đường kéo dài, gây kẹt xe giờ cao điểm.', 2, N'encroachment', 0.8790, 0, geography::STGeomFromText('POINT (106.803 10.964)', 4326), N'Đường Huỳnh Văn Nghệ, khu vực Bửu Long, phường Trấn Biên', 21, 27, 7, NULL, 0, 3, 1, 3, '2026-09-29T14:14:00', '2026-09-27T19:14:00', 0, NULL, '2026-09-29T01:14:00', '2026-09-27T14:12:00', '2026-09-29T01:14:00', '2026-09-27T14:12:10'),
 (8, 'DN-2026-B6SJ', 13, '0903000001', N'Nước thải màu lạ chảy ra mương thoát nước gần khu công nghiệp.', 4, N'illegal_dump', 0.8255, 0, geography::STGeomFromText('POINT (106.842 10.92)', 4326), N'Khu công nghiệp Biên Hòa 1, khu vực An Bình, phường Trấn Biên', 40, 39, 17, NULL, 0, 3, 3, 1, '2026-09-29T20:14:00', '2026-09-29T16:34:00', 0, NULL, '2026-09-29T20:04:00', '2026-09-29T16:12:00', '2026-09-29T20:04:00', '2026-09-29T16:12:10'),
 (9, 'DN-2026-Y3HP', NULL, '0388123456', N'Rác thải bị vứt bừa bãi dọc bờ kè, lâu ngày không ai thu gom.', 1, N'waste', 0.9150, 0, geography::STGeomFromText('POINT (106.813 10.923)', 4326), N'Khu vực Bửu Hòa, phường Biên Hòa', 11, 19, 18, NULL, 0, 3, 2, 2, '2026-10-03T04:14:00', '2026-10-02T05:44:00', 1, NULL, '2026-10-03T11:44:00', '2026-10-02T04:12:00', '2026-10-03T11:44:00', '2026-10-02T04:12:10'),
 (10, 'DN-2026-C8YZ', NULL, '0922333444', N'Xe ô tô đậu trước cửa nhà chắn lối ra vào.', 2, N'encroachment', 0.8610, 0, geography::STGeomFromText('POINT (106.825 10.947)', 4326), N'Đường Hà Huy Giáp, phường Trấn Biên', 30, 32, 15, NULL, 0, 4, 1, 3, '2026-10-05T00:14:00', NULL, 0, NULL, NULL, '2026-10-03T00:12:00', '2026-10-03T01:49:00', '2026-10-03T00:12:10'),
 (11, 'DN-2026-N8RA', 13, '0903000001', N'Nắp hố ga bị vỡ, có nguy cơ gây tai nạn cho người đi đường.', 3, N'infrastructure', 0.8930, 0, geography::STGeomFromText('POINT (106.854 10.9375)', 4326), N'Gần chợ Bình Đa, phường Tam Hiệp', 51, 46, 12, NULL, 0, 3, 3, 1, '2026-10-04T10:14:00', '2026-10-04T09:34:00', 0, '2026-10-04T09:26:00', '2026-10-04T10:04:00', '2026-10-04T06:12:00', '2026-10-04T10:04:00', '2026-10-04T06:12:10'),
 (12, 'DN-2026-G3VU', NULL, '0919345678', N'Rác ứ đọng tại điểm hẹn thu gom, xe rác không đến đúng giờ.', 1, N'waste', 0.9300, 0, geography::STGeomFromText('POINT (106.85 10.958)', 4326), N'Khu vực Tân Hiệp, phường Tam Hiệp', 45, 42, 11, NULL, 0, 3, 2, 2, '2026-10-06T12:14:00', '2026-10-05T12:44:00', 0, NULL, '2026-10-05T16:44:00', '2026-10-05T12:12:00', '2026-10-05T16:44:00', '2026-10-05T12:12:10'),
 (13, 'DN-2026-C5UE', NULL, '0912000777', N'Thùng rác công cộng đầy tràn, rác rơi ra vỉa hè.', 1, N'waste', 0.9405, 0, geography::STGeomFromText('POINT (106.816 10.9545)', 4326), N'Đường Phan Đình Phùng, khu vực Quang Vinh, phường Trấn Biên', 28, 31, 8, NULL, 0, 3, 2, 2, '2026-10-07T08:14:00', '2026-10-06T08:59:00', 0, NULL, '2026-10-06T13:59:00', '2026-10-06T08:12:00', '2026-10-06T13:59:00', '2026-10-06T08:12:10'),
 (14, 'DN-2026-B8NH', NULL, '0926789123', N'Lan can cầu đi bộ bị gãy, nguy hiểm cho trẻ em.', 3, N'infrastructure', 0.9050, 0, geography::STGeomFromText('POINT (106.838 10.94)', 4326), N'Khu vực Thống Nhất, phường Trấn Biên', 35, 35, 16, NULL, 0, 3, 3, 1, '2026-10-06T22:14:00', '2026-10-06T18:26:00', 0, NULL, '2026-10-06T21:26:00', '2026-10-06T18:12:00', '2026-10-06T21:26:00', '2026-10-06T18:12:10'),
 (15, 'DN-2026-F7KX', NULL, '0356789012', N'Bán hàng rong, họp chợ tự phát lấn chiếm lòng đường vào buổi sáng.', 2, N'encroachment', 0.8520, 0, geography::STGeomFromText('POINT (106.785 10.948)', 4326), N'Khu vực Tân Hạnh, phường Biên Hòa', 1, 12, 9, NULL, 0, 3, 1, 3, '2026-10-09T08:14:00', '2026-10-07T12:14:00', 0, NULL, '2026-10-08T14:14:00', '2026-10-07T08:12:00', '2026-10-08T14:14:00', '2026-10-07T08:12:10'),
 (16, 'DN-2026-Q2WD', 13, '0903000001', N'Xà bần công trình bị đổ trộm trên vỉa hè đường Đồng Khởi.', 1, N'waste', 0.9230, 0, geography::STGeomFromText('POINT (106.856 10.961)', 4326), N'Đường Đồng Khởi, khu vực Tân Hiệp, phường Tam Hiệp', 45, 42, 11, NULL, 0, 3, 2, 2, '2026-10-09T08:14:00', '2026-10-08T08:49:00', 0, NULL, '2026-10-08T16:49:00', '2026-10-08T08:12:00', '2026-10-08T16:49:00', '2026-10-08T08:12:10'),
 (17, 'DN-2026-W2JA', NULL, '0911888999', N'Rác cạnh nhà dân.', 1, N'waste', 0.8800, 0, geography::STGeomFromText('POINT (106.79 10.938)', 4326), N'Khu vực Hóa An, phường Biên Hòa', 5, 15, 9, NULL, 0, 4, 2, 2, '2026-10-09T20:14:00', NULL, 0, NULL, NULL, '2026-10-08T20:12:00', '2026-10-08T21:49:00', '2026-10-08T20:12:10'),
 (18, 'DN-2026-J9ZT', NULL, '0901234987', N'Cây xanh gãy cành lớn đè lên vỉa hè sau cơn mưa.', 3, N'infrastructure', 0.9010, 0, geography::STGeomFromText('POINT (106.83 10.943)', 4326), N'Đường Võ Thị Sáu, khu vực Thống Nhất, phường Trấn Biên', 33, 34, 15, NULL, 0, 3, 3, 1, '2026-10-09T10:14:00', '2026-10-09T06:24:00', 0, NULL, '2026-10-09T08:12:00', '2026-10-09T06:12:00', '2026-10-09T08:12:00', '2026-10-09T06:12:10'),
 (19, 'DN-2026-Y6KD', NULL, '0945612378', N'Bãi giữ xe tự phát chiếm vỉa hè trước khu dân cư.', 2, N'encroachment', 0.8700, 0, geography::STGeomFromText('POINT (106.825 10.905)', 4326), N'Khu vực Tân Vạn, giáp Dĩ An, phường Biên Hòa', 19, 25, 19, NULL, 0, 3, 1, 3, '2026-10-11T11:14:00', '2026-10-09T13:44:00', 0, NULL, '2026-10-10T11:44:00', '2026-10-09T11:12:00', '2026-10-10T11:44:00', '2026-10-09T11:12:10'),
 (20, 'DN-2026-G8DS', NULL, '0987654321', N'Tiệm sửa xe để xe và đồ nghề chiếm toàn bộ vỉa hè.', 2, N'encroachment', 0.8960, 0, geography::STGeomFromText('POINT (106.842 10.9505)', 4326), N'Đường Phan Trung, khu vực Tân Mai, phường Tam Hiệp', 47, 43, 11, NULL, 0, 2, 1, 3, '2026-10-12T02:14:00', '2026-10-10T04:14:00', 0, NULL, NULL, '2026-10-10T02:12:00', '2026-10-10T04:14:00', '2026-10-10T02:12:10'),
 (21, 'DN-2026-V4MB', NULL, '0944556677', N'Xe bồn xả dầu nhớt thải xuống cống thoát nước vào ban đêm.', 4, N'illegal_dump', 0.8710, 0, geography::STGeomFromText('POINT (106.804 10.93)', 4326), N'Đường Nguyễn Tri Phương, khu vực Bửu Hòa, phường Biên Hòa', 9, 18, 10, NULL, 0, 3, 3, 1, '2026-10-10T06:14:00', '2026-10-10T02:39:00', 0, NULL, '2026-10-10T05:39:00', '2026-10-10T02:12:00', '2026-10-10T05:39:00', '2026-10-10T02:12:10'),
 (22, 'DN-2026-E4RM', 13, '0903000001', N'Rác sinh hoạt bỏ cạnh cổng trường học, chưa được thu gom.', 1, N'waste', 0.9000, 0, geography::STGeomFromText('POINT (106.823 10.952)', 4326), N'Đường 30 Tháng 4, khu vực Trung Dũng, phường Trấn Biên', 28, 31, 8, NULL, 1, 1, 2, 2, '2026-10-11T06:14:00', NULL, 1, '2026-10-11T01:26:00', NULL, '2026-10-10T06:12:00', '2026-10-11T06:14:00', '2026-10-10T06:12:10'),
 (23, 'DN-2026-X6PL', 13, '0903000001', N'Điểm tập kết rác tạm chưa được thu gom, rác tràn ra đường.', 1, N'waste', 0.9110, 0, geography::STGeomFromText('POINT (106.829 10.958)', 4326), N'Đường Nguyễn Ái Quốc, khu vực Trung Dũng, phường Trấn Biên', 25, 29, 8, NULL, 1, 2, 2, 2, '2026-10-11T12:14:00', '2026-10-10T13:04:00', 1, NULL, NULL, '2026-10-10T12:12:00', '2026-10-11T12:14:42', '2026-10-10T12:12:10'),
 (24, 'DN-2026-Q5HS', NULL, '0908123123', N'Túi rác bị vứt xuống mương thoát nước, gây tắc nghẽn.', 1, N'waste', 0.9180, 0, geography::STGeomFromText('POINT (106.812 10.96)', 4326), N'Khu vực Bửu Long, phường Trấn Biên', 24, 28, 7, NULL, 0, 3, 2, 2, '2026-10-11T17:14:00', '2026-10-10T17:44:00', 0, NULL, '2026-10-10T20:44:00', '2026-10-10T17:12:00', '2026-10-10T20:44:00', '2026-10-10T17:12:10'),
 (25, 'DN-2026-U8BL', NULL, '0399887766', N'Dựng rạp đám cưới chiếm toàn bộ lòng đường trong hẻm.', 2, N'encroachment', 0.8800, 0, geography::STGeomFromText('POINT (106.862 10.94)', 4326), N'Khu vực Bình Đa, phường Tam Hiệp', 52, 47, 20, NULL, 0, 1, 1, 3, '2026-10-12T20:14:00', NULL, 0, NULL, NULL, '2026-10-10T20:12:00', '2026-10-10T20:14:00', '2026-10-10T20:12:10'),
 (26, 'DN-2026-S5HV', NULL, '0917771234', N'Rác thải công nghiệp bị bỏ lại ven đường.', 1, N'waste', 0.8880, 0, geography::STGeomFromText('POINT (106.8475 10.9235)', 4326), N'Đường Trần Quốc Toản, khu vực An Bình, phường Trấn Biên', 38, 37, 16, NULL, 0, 2, 2, 2, '2026-10-11T22:14:00', '2026-10-11T01:14:00', 0, NULL, NULL, '2026-10-10T22:12:00', '2026-10-11T01:14:00', '2026-10-10T22:12:10'),
 (27, 'DN-2026-N2CT', 13, '0903000001', N'Xưởng nhỏ xả khói và nước thải ra hẻm dân cư.', 4, N'illegal_dump', 0.8900, 0, geography::STGeomFromText('POINT (106.86 10.933)', 4326), N'Khu vực Tam Hiệp, phường Tam Hiệp', 51, 46, 12, NULL, 0, 3, 3, 1, '2026-10-11T04:14:00', '2026-10-11T00:29:00', 0, NULL, '2026-10-11T02:41:00', '2026-10-11T00:12:00', '2026-10-11T02:41:00', '2026-10-11T00:12:10'),
 (28, 'DN-2026-L3TN', NULL, '0971239876', N'Mặt đường sụt lún tạo hố sâu gần chân cầu Hóa An.', 3, N'infrastructure', 0.9270, 0, geography::STGeomFromText('POINT (106.8 10.945)', 4326), N'Gần chân cầu Hóa An, phường Biên Hòa', 6, 16, 10, NULL, 1, 2, 3, 1, '2026-10-11T06:14:00', '2026-10-11T02:34:00', 1, NULL, NULL, '2026-10-11T02:12:00', '2026-10-11T06:14:00', '2026-10-11T02:12:10'),
 (29, 'DN-2026-A9FW', NULL, '0932145678', N'Người dân đổ rác không đúng giờ, rác chất đống ở đầu hẻm.', 1, N'waste', 0.9060, 0, geography::STGeomFromText('POINT (106.818 10.915)', 4326), N'Khu vực Tân Vạn, phường Biên Hòa', 15, 22, 18, NULL, 0, 1, 2, 2, '2026-10-12T03:14:00', NULL, 0, NULL, NULL, '2026-10-11T03:12:00', '2026-10-11T03:14:00', '2026-10-11T03:12:10'),
 (30, 'DN-2026-K8TD', 13, '0903000001', N'Chợ tự phát họp trên đường, lấn chiếm lòng đường buổi sáng.', 2, N'encroachment', 0.8900, 1, geography::STGeomFromText('POINT (106.865 10.985)', 4326), N'Đường Bùi Trọng Nghĩa, phường Trảng Dài', NULL, 8, NULL, NULL, 0, 0, 1, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-11T04:12:00', '2026-10-11T04:12:00', '2026-10-11T04:12:10'),
 (31, 'DN-2026-Z7KC', NULL, '0905558899', N'Nước thải từ nhà hàng chảy tràn ra đường, mùi hôi.', 4, N'illegal_dump', 0.8590, 0, geography::STGeomFromText('POINT (106.7985 10.9665)', 4326), N'Đường Huỳnh Văn Nghệ, gần khu du lịch Bửu Long, phường Trấn Biên', 20, 26, 7, NULL, 0, 1, 3, 1, '2026-10-11T08:38:00', NULL, 1, '2026-10-11T07:50:00', NULL, '2026-10-11T04:36:00', '2026-10-11T08:38:47', '2026-10-11T04:36:10'),
 (32, 'DN-2026-D2QX', 13, '0903000001', N'Đèn tín hiệu giao thông tại giao lộ bị tắt, xe cộ lộn xộn.', 3, N'infrastructure', 0.9180, 0, geography::STGeomFromText('POINT (106.859 10.945)', 4326), N'Đường Phạm Văn Thuận, gần chợ Tam Hiệp, phường Tam Hiệp', 49, 44, 12, NULL, 0, 1, 3, 1, '2026-10-11T10:14:00', NULL, 1, '2026-10-11T09:26:51', NULL, '2026-10-11T06:12:00', '2026-10-11T10:14:55', '2026-10-11T06:12:10'),
 (33, 'DN-2026-H5NC', NULL, '0918999000', N'Rác thải tập kết sai quy định dọc Quốc lộ 1A.', 1, N'waste', 0.9120, 1, geography::STGeomFromText('POINT (106.878 10.953)', 4326), N'Quốc lộ 1A, gần ngã tư Amata, phường Long Bình', NULL, 4, NULL, NULL, 0, 0, 2, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-11T06:42:00', '2026-10-11T06:42:00', '2026-10-11T06:42:10'),
 (34, 'DN-2026-M6QS', NULL, '0977000111', N'Có vật lạ chắn ngang lối đi, chưa rõ của ai.', NULL, N'other', 0.4100, 1, geography::STGeomFromText('POINT (106.821 10.956)', 4326), N'Khu vực Quang Vinh, phường Trấn Biên', 28, 31, NULL, NULL, 0, 0, 2, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-11T07:24:00', '2026-10-11T07:24:00', '2026-10-11T07:24:10'),
 (35, 'DN-2026-R3BX', NULL, '0355123987', N'Vỉa hè bị bong tróc nhiều chỗ.', NULL, N'infrastructure', 0.5200, 1, geography::STGeomFromText('POINT (106.853 10.948)', 4326), N'Khu vực Tân Mai, phường Tam Hiệp', 49, 44, NULL, NULL, 0, 0, 2, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-11T07:36:00', '2026-10-11T07:36:00', '2026-10-11T07:36:10'),
 (36, 'DN-2026-P9WE', NULL, '0909345678', N'Xe chở bùn thải làm rơi vãi, nước bẩn chảy trên Quốc lộ 51.', 4, N'illegal_dump', 0.8800, 1, geography::STGeomFromText('POINT (106.87 10.89)', 4326), N'Quốc lộ 51, phường Long Hưng', NULL, 5, NULL, NULL, 0, 0, 3, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-11T07:48:00', '2026-10-11T07:48:00', '2026-10-11T07:48:10'),
 (37, 'DN-2026-T2LG', NULL, '0938765432', N'Cả dãy đèn đường khu vòng xoay Tam Hiệp bị tắt.', 3, N'infrastructure', 0.9200, 0, geography::STGeomFromText('POINT (106.86405 10.92905)', 4326), N'Vòng xoay Tam Hiệp, phường Tam Hiệp', 53, 48, NULL, NULL, 0, 3, 3, NULL, NULL, NULL, 0, NULL, '2026-09-25T14:59:00', '2026-09-25T13:12:00', '2026-09-25T14:59:00', '2026-09-25T13:12:10'),
 (38, 'DN-2026-E5RN', NULL, '0909111222', N'Rác để lâu ngày cạnh cổng trường, bốc mùi.', 1, N'waste', 0.8950, 0, geography::STGeomFromText('POINT (106.82305 10.95205)', 4326), N'Đường 30 Tháng 4, phường Trấn Biên', 28, 31, NULL, NULL, 0, 0, 2, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-10T07:12:00', '2026-10-10T07:12:00', '2026-10-10T07:12:10'),
 (39, 'DN-2026-X7PM', NULL, '0911222333', N'Rác tràn ra lòng đường Nguyễn Ái Quốc, xe qua lại khó khăn.', 1, N'waste', 0.9020, 0, geography::STGeomFromText('POINT (106.82895 10.9581)', 4326), N'Đường Nguyễn Ái Quốc, phường Trấn Biên', 25, 29, NULL, NULL, 0, 0, 2, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-10T13:12:00', '2026-10-10T13:12:00', '2026-10-10T13:12:10'),
 (40, 'DN-2026-L4TP', NULL, '0966777888', N'Hố sụt lún giữa đường gần cầu Hóa An rất nguy hiểm.', 3, N'infrastructure', 0.9110, 0, geography::STGeomFromText('POINT (106.80008 10.94505)', 4326), N'Gần cầu Hóa An, phường Biên Hòa', 6, 16, NULL, NULL, 0, 0, 3, NULL, NULL, NULL, 0, NULL, NULL, '2026-10-11T02:42:00', '2026-10-11T02:42:00', '2026-10-11T02:42:10');
SET IDENTITY_INSERT dbo.tbl_incidents OFF;
-- Ticket cha (gom trung)
UPDATE i SET parent_incident_id = m.parent_id FROM dbo.tbl_incidents i JOIN (VALUES
 (37, 6),
 (38, 22),
 (39, 23),
 (40, 28)
) m (id, parent_id) ON m.id = i.id;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_incident_images (60 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_incident_images ON;
INSERT INTO dbo.tbl_incident_images ([id], [incident_id], [image_type], [image_url], [capture_point], [uploaded_by_user_id], [uploaded_at]) VALUES
 (1, 1, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.8195 10.9475)', 4326), 13, '2026-09-14T06:12:00'),
 (2, 2, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.795 10.94)', 4326), NULL, '2026-09-16T08:12:00'),
 (3, 3, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.845 10.955)', 4326), NULL, '2026-09-18T20:12:00'),
 (4, 4, 'BEFORE', N'/uploads/seed/illegal_dump_before.jpg', geography::STGeomFromText('POINT (106.833 10.933)', 4326), 13, '2026-09-21T08:12:00'),
 (5, 5, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.823 10.91)', 4326), NULL, '2026-09-23T10:12:00'),
 (6, 6, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.864 10.929)', 4326), NULL, '2026-09-25T12:12:00'),
 (7, 7, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.803 10.964)', 4326), NULL, '2026-09-27T14:12:00'),
 (8, 8, 'BEFORE', N'/uploads/seed/illegal_dump_before.jpg', geography::STGeomFromText('POINT (106.842 10.92)', 4326), 13, '2026-09-29T16:12:00'),
 (9, 9, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.813 10.923)', 4326), NULL, '2026-10-02T04:12:00'),
 (10, 10, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.825 10.947)', 4326), NULL, '2026-10-03T00:12:00'),
 (11, 11, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.854 10.9375)', 4326), 13, '2026-10-04T06:12:00'),
 (12, 12, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.85 10.958)', 4326), NULL, '2026-10-05T12:12:00'),
 (13, 13, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.816 10.9545)', 4326), NULL, '2026-10-06T08:12:00'),
 (14, 14, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.838 10.94)', 4326), NULL, '2026-10-06T18:12:00'),
 (15, 15, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.785 10.948)', 4326), NULL, '2026-10-07T08:12:00'),
 (16, 16, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.856 10.961)', 4326), 13, '2026-10-08T08:12:00'),
 (17, 17, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.79 10.938)', 4326), NULL, '2026-10-08T20:12:00'),
 (18, 18, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.83 10.943)', 4326), NULL, '2026-10-09T06:12:00'),
 (19, 19, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.825 10.905)', 4326), NULL, '2026-10-09T11:12:00'),
 (20, 20, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.842 10.9505)', 4326), NULL, '2026-10-10T02:12:00'),
 (21, 21, 'BEFORE', N'/uploads/seed/illegal_dump_before.jpg', geography::STGeomFromText('POINT (106.804 10.93)', 4326), NULL, '2026-10-10T02:12:00'),
 (22, 22, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.823 10.952)', 4326), 13, '2026-10-10T06:12:00'),
 (23, 23, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.829 10.958)', 4326), 13, '2026-10-10T12:12:00'),
 (24, 24, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.812 10.96)', 4326), NULL, '2026-10-10T17:12:00'),
 (25, 25, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.862 10.94)', 4326), NULL, '2026-10-10T20:12:00'),
 (26, 26, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.8475 10.9235)', 4326), NULL, '2026-10-10T22:12:00'),
 (27, 27, 'BEFORE', N'/uploads/seed/illegal_dump_before.jpg', geography::STGeomFromText('POINT (106.86 10.933)', 4326), 13, '2026-10-11T00:12:00'),
 (28, 28, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.8 10.945)', 4326), NULL, '2026-10-11T02:12:00'),
 (29, 29, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.818 10.915)', 4326), NULL, '2026-10-11T03:12:00'),
 (30, 30, 'BEFORE', N'/uploads/seed/encroachment_before.jpg', geography::STGeomFromText('POINT (106.865 10.985)', 4326), 13, '2026-10-11T04:12:00'),
 (31, 31, 'BEFORE', N'/uploads/seed/illegal_dump_before.jpg', geography::STGeomFromText('POINT (106.7985 10.9665)', 4326), NULL, '2026-10-11T04:36:00'),
 (32, 32, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.859 10.945)', 4326), 13, '2026-10-11T06:12:00'),
 (33, 33, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.878 10.953)', 4326), NULL, '2026-10-11T06:42:00'),
 (34, 34, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.821 10.956)', 4326), NULL, '2026-10-11T07:24:00'),
 (35, 35, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.853 10.948)', 4326), NULL, '2026-10-11T07:36:00'),
 (36, 36, 'BEFORE', N'/uploads/seed/illegal_dump_before.jpg', geography::STGeomFromText('POINT (106.87 10.89)', 4326), NULL, '2026-10-11T07:48:00'),
 (37, 37, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.86405 10.92905)', 4326), NULL, '2026-09-25T13:12:00'),
 (38, 38, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.82305 10.95205)', 4326), NULL, '2026-10-10T07:12:00'),
 (39, 39, 'BEFORE', N'/uploads/seed/waste_before.jpg', geography::STGeomFromText('POINT (106.82895 10.9581)', 4326), NULL, '2026-10-10T13:12:00'),
 (40, 40, 'BEFORE', N'/uploads/seed/infrastructure_before.jpg', geography::STGeomFromText('POINT (106.80008 10.94505)', 4326), NULL, '2026-10-11T02:42:00'),
 (41, 8, 'AFTER', N'/uploads/seed/illegal_dump_after.jpg', geography::STGeomFromText('POINT (106.84197 10.92004)', 4326), 17, '2026-09-29T20:04:00'),
 (42, 14, 'AFTER', N'/uploads/seed/infrastructure_after.jpg', geography::STGeomFromText('POINT (106.83797 10.94004)', 4326), 16, '2026-10-06T21:26:00'),
 (43, 13, 'AFTER', N'/uploads/seed/waste_after.jpg', geography::STGeomFromText('POINT (106.81597000000001 10.95454)', 4326), 8, '2026-10-06T13:59:00'),
 (44, 15, 'AFTER', N'/uploads/seed/encroachment_after.jpg', geography::STGeomFromText('POINT (106.78497 10.94804)', 4326), 9, '2026-10-08T14:14:00'),
 (45, 12, 'AFTER', N'/uploads/seed/waste_after.jpg', geography::STGeomFromText('POINT (106.84997 10.95804)', 4326), 11, '2026-10-05T16:44:00'),
 (46, 4, 'AFTER', N'/uploads/seed/illegal_dump_after.jpg', geography::STGeomFromText('POINT (106.83297 10.93304)', 4326), 15, '2026-09-21T14:14:00'),
 (47, 18, 'AFTER', N'/uploads/seed/infrastructure_after.jpg', geography::STGeomFromText('POINT (106.82997 10.94304)', 4326), 15, '2026-10-09T08:12:00'),
 (48, 1, 'AFTER', N'/uploads/seed/waste_after.jpg', geography::STGeomFromText('POINT (106.81947000000001 10.94754)', 4326), 8, '2026-09-14T12:39:00'),
 (49, 7, 'AFTER', N'/uploads/seed/encroachment_after.jpg', geography::STGeomFromText('POINT (106.80297 10.96404)', 4326), 7, '2026-09-29T01:14:00'),
 (50, 27, 'AFTER', N'/uploads/seed/illegal_dump_after.jpg', geography::STGeomFromText('POINT (106.85997 10.93304)', 4326), 12, '2026-10-11T02:41:00'),
 (51, 11, 'AFTER', N'/uploads/seed/infrastructure_after.jpg', geography::STGeomFromText('POINT (106.85397 10.93754)', 4326), 12, '2026-10-04T10:04:00'),
 (52, 2, 'AFTER', N'/uploads/seed/infrastructure_after.jpg', geography::STGeomFromText('POINT (106.79497 10.94004)', 4326), 9, '2026-09-16T12:06:00'),
 (53, 16, 'AFTER', N'/uploads/seed/waste_after.jpg', geography::STGeomFromText('POINT (106.85597 10.96104)', 4326), 11, '2026-10-08T16:49:00'),
 (54, 24, 'AFTER', N'/uploads/seed/waste_after.jpg', geography::STGeomFromText('POINT (106.81197 10.960040000000001)', 4326), 7, '2026-10-10T20:44:00'),
 (55, 3, 'AFTER', N'/uploads/seed/encroachment_after.jpg', geography::STGeomFromText('POINT (106.84497 10.95504)', 4326), 11, '2026-09-19T19:14:00'),
 (56, 6, 'AFTER', N'/uploads/seed/infrastructure_after.jpg', geography::STGeomFromText('POINT (106.86397000000001 10.92904)', 4326), 20, '2026-09-25T14:59:00'),
 (57, 21, 'AFTER', N'/uploads/seed/illegal_dump_after.jpg', geography::STGeomFromText('POINT (106.80397 10.93004)', 4326), 10, '2026-10-10T05:39:00'),
 (58, 5, 'AFTER', N'/uploads/seed/waste_after.jpg', geography::STGeomFromText('POINT (106.82297 10.91004)', 4326), 18, '2026-09-24T05:14:00'),
 (59, 9, 'AFTER', N'/uploads/seed/waste_after.jpg', geography::STGeomFromText('POINT (106.81297 10.92304)', 4326), 18, '2026-10-03T11:44:00'),
 (60, 19, 'AFTER', N'/uploads/seed/encroachment_after.jpg', geography::STGeomFromText('POINT (106.82497000000001 10.90504)', 4326), 19, '2026-10-10T11:44:00');
SET IDENTITY_INSERT dbo.tbl_incident_images OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_incident_logs (217 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_incident_logs ON;
INSERT INTO dbo.tbl_incident_logs ([id], [incident_id], [action_by_user_id], [action_type], [action_note], [old_status], [new_status], [created_at]) VALUES
 (1, 29, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T03:12:00'),
 (2, 8, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-29T16:12:00'),
 (3, 14, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-06T18:12:00'),
 (4, 13, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-06T08:12:00'),
 (5, 10, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-03T00:12:00'),
 (6, 32, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T06:12:00'),
 (7, 22, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T06:12:00'),
 (8, 38, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T07:12:00'),
 (9, 15, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-07T08:12:00'),
 (10, 12, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-05T12:12:00'),
 (11, 20, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T02:12:00'),
 (12, 4, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-21T08:12:00'),
 (13, 33, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T06:42:00'),
 (14, 18, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-09T06:12:00'),
 (15, 1, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-14T06:12:00'),
 (16, 30, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T04:12:00'),
 (17, 28, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T02:12:00'),
 (18, 40, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T02:42:00'),
 (19, 7, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-27T14:12:00'),
 (20, 34, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T07:24:00'),
 (21, 27, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T00:12:00'),
 (22, 11, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-04T06:12:00'),
 (23, 2, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-16T08:12:00'),
 (24, 36, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T07:48:00'),
 (25, 16, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-08T08:12:00'),
 (26, 24, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T17:12:00'),
 (27, 35, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T07:36:00'),
 (28, 3, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-18T20:12:00'),
 (29, 26, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T22:12:00'),
 (30, 37, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-25T13:12:00'),
 (31, 6, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-25T12:12:00'),
 (32, 25, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T20:12:00'),
 (33, 21, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T02:12:00'),
 (34, 17, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-08T20:12:00'),
 (35, 5, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-09-23T10:12:00'),
 (36, 23, 13, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T12:12:00'),
 (37, 39, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-10T13:12:00'),
 (38, 9, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-02T04:12:00'),
 (39, 19, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-09T11:12:00'),
 (40, 31, NULL, 'CREATED', N'Tiếp nhận phản ánh từ công dân.', NULL, 0, '2026-10-11T04:36:00'),
 (41, 29, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP11-G01 - Khu phố 11 (ô 1), Phường Biên Hòa.', NULL, NULL, '2026-10-11T03:12:04'),
 (42, 8, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP14-G01 - Khu phố 14 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-09-29T16:12:04'),
 (43, 14, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP10-G01 - Khu phố 10, Phường Trấn Biên.', NULL, NULL, '2026-10-06T18:12:04'),
 (44, 13, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP06-G01 - Khu phố 6 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-10-06T08:12:04'),
 (45, 10, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP07-G01 - Khu phố 7 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-10-03T00:12:04'),
 (46, 32, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP04-G02 - Khu phố 4 (ô 2), Phường Tam Hiệp.', NULL, NULL, '2026-10-11T06:12:04'),
 (47, 22, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP06-G01 - Khu phố 6 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-10-10T06:12:04'),
 (48, 38, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP06-G01 - Khu phố 6 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-10-10T07:12:04'),
 (49, 15, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP01-G01 - Khu phố 1, Phường Biên Hòa.', NULL, NULL, '2026-10-07T08:12:04'),
 (50, 12, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP02-G01 - Khu phố 2 (ô 1), Phường Tam Hiệp.', NULL, NULL, '2026-10-05T12:12:04'),
 (51, 20, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP03-G01 - Khu phố 3, Phường Tam Hiệp.', NULL, NULL, '2026-10-10T02:12:04'),
 (52, 4, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP09-G02 - Khu phố 9 (ô 2), Phường Trấn Biên.', NULL, NULL, '2026-09-21T08:12:04'),
 (53, 33, NULL, 'GRID_MATCHED', N'Không thuộc ô lưới nào. Thuộc đơn vị Phường Long Bình, chuyển điều phối viên.', NULL, NULL, '2026-10-11T06:42:04'),
 (54, 18, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP09-G01 - Khu phố 9 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-10-09T06:12:04'),
 (55, 1, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP06-G02 - Khu phố 6 (ô 2), Phường Trấn Biên.', NULL, NULL, '2026-09-14T06:12:04'),
 (56, 30, NULL, 'GRID_MATCHED', N'Không thuộc ô lưới nào. Thuộc đơn vị Phường Trảng Dài, chuyển điều phối viên.', NULL, NULL, '2026-10-11T04:12:04'),
 (57, 28, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP05-G01 - Khu phố 5, Phường Biên Hòa.', NULL, NULL, '2026-10-11T02:12:04'),
 (58, 40, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP05-G01 - Khu phố 5, Phường Biên Hòa.', NULL, NULL, '2026-10-11T02:42:04'),
 (59, 7, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP02-G01 - Khu phố 2 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-09-27T14:12:04'),
 (60, 34, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP06-G01 - Khu phố 6 (ô 1), Phường Trấn Biên.', NULL, NULL, '2026-10-11T07:24:04'),
 (61, 27, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP06-G01 - Khu phố 6, Phường Tam Hiệp.', NULL, NULL, '2026-10-11T00:12:04'),
 (62, 11, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP06-G01 - Khu phố 6, Phường Tam Hiệp.', NULL, NULL, '2026-10-04T06:12:04'),
 (63, 2, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP04-G01 - Khu phố 4 (ô 1), Phường Biên Hòa.', NULL, NULL, '2026-09-16T08:12:04'),
 (64, 36, NULL, 'GRID_MATCHED', N'Không thuộc ô lưới nào. Thuộc đơn vị Phường Long Hưng, chuyển điều phối viên.', NULL, NULL, '2026-10-11T07:48:04'),
 (65, 16, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP02-G01 - Khu phố 2 (ô 1), Phường Tam Hiệp.', NULL, NULL, '2026-10-08T08:12:04'),
 (66, 24, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP03-G02 - Khu phố 3 (ô 2), Phường Trấn Biên.', NULL, NULL, '2026-10-10T17:12:04'),
 (67, 35, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP04-G02 - Khu phố 4 (ô 2), Phường Tam Hiệp.', NULL, NULL, '2026-10-11T07:36:04'),
 (68, 3, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP03-G01 - Khu phố 3, Phường Tam Hiệp.', NULL, NULL, '2026-09-18T20:12:04'),
 (69, 26, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP12-G02 - Khu phố 12 (ô 2), Phường Trấn Biên.', NULL, NULL, '2026-10-10T22:12:04'),
 (70, 37, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP08-G01 - Khu phố 8 (ô 1), Phường Tam Hiệp.', NULL, NULL, '2026-09-25T13:12:04'),
 (71, 6, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP08-G01 - Khu phố 8 (ô 1), Phường Tam Hiệp.', NULL, NULL, '2026-09-25T12:12:04'),
 (72, 25, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TH-KP07-G01 - Khu phố 7, Phường Tam Hiệp.', NULL, NULL, '2026-10-10T20:12:04'),
 (73, 21, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP07-G01 - Khu phố 7 (ô 1), Phường Biên Hòa.', NULL, NULL, '2026-10-10T02:12:04'),
 (74, 17, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP04-G02 - Khu phố 4 (ô 2), Phường Biên Hòa.', NULL, NULL, '2026-10-08T20:12:04'),
 (75, 5, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP11-G02 - Khu phố 11 (ô 2), Phường Biên Hòa.', NULL, NULL, '2026-09-23T10:12:04'),
 (76, 23, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP04-G01 - Khu phố 4, Phường Trấn Biên.', NULL, NULL, '2026-10-10T12:12:04'),
 (77, 39, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP04-G01 - Khu phố 4, Phường Trấn Biên.', NULL, NULL, '2026-10-10T13:12:04'),
 (78, 9, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP08-G01 - Khu phố 8, Phường Biên Hòa.', NULL, NULL, '2026-10-02T04:12:04'),
 (79, 19, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-BH-KP14-G01 - Khu phố 14, Phường Biên Hòa.', NULL, NULL, '2026-10-09T11:12:04'),
 (80, 31, NULL, 'GRID_MATCHED', N'Thuộc ô lưới DNI-TB-KP01-G01 - Khu phố 1, Phường Trấn Biên.', NULL, NULL, '2026-10-11T04:36:04'),
 (81, 29, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.91).', NULL, NULL, '2026-10-11T03:12:08'),
 (82, 8, NULL, 'AI_CLASSIFIED', N'AI phân loại: Xả thải (độ tin cậy 0.83).', NULL, NULL, '2026-09-29T16:12:08'),
 (83, 14, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.91).', NULL, NULL, '2026-10-06T18:12:08'),
 (84, 13, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.94).', NULL, NULL, '2026-10-06T08:12:08'),
 (85, 10, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.86).', NULL, NULL, '2026-10-03T00:12:08'),
 (86, 32, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.92).', NULL, NULL, '2026-10-11T06:12:08'),
 (87, 22, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.90).', NULL, NULL, '2026-10-10T06:12:08'),
 (88, 38, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.90).', NULL, NULL, '2026-10-10T07:12:08'),
 (89, 15, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.85).', NULL, NULL, '2026-10-07T08:12:08'),
 (90, 12, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.93).', NULL, NULL, '2026-10-05T12:12:08'),
 (91, 20, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.90).', NULL, NULL, '2026-10-10T02:12:08'),
 (92, 4, NULL, 'AI_CLASSIFIED', N'AI phân loại: Xả thải (độ tin cậy 0.86).', NULL, NULL, '2026-09-21T08:12:08'),
 (93, 33, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.91).', NULL, NULL, '2026-10-11T06:42:08'),
 (94, 18, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.90).', NULL, NULL, '2026-10-09T06:12:08'),
 (95, 1, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.93).', NULL, NULL, '2026-09-14T06:12:08'),
 (96, 30, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.89).', NULL, NULL, '2026-10-11T04:12:08'),
 (97, 28, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.93).', NULL, NULL, '2026-10-11T02:12:08'),
 (98, 40, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.91).', NULL, NULL, '2026-10-11T02:42:08'),
 (99, 7, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.88).', NULL, NULL, '2026-09-27T14:12:08'),
 (100, 34, NULL, 'AI_CLASSIFIED', N'AI không chắc chắn (nhãn other, độ tin cậy 0.41). Chuyển điều phối viên.', NULL, NULL, '2026-10-11T07:24:08'),
 (101, 27, NULL, 'AI_CLASSIFIED', N'AI phân loại: Xả thải (độ tin cậy 0.89).', NULL, NULL, '2026-10-11T00:12:08'),
 (102, 11, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.89).', NULL, NULL, '2026-10-04T06:12:08'),
 (103, 2, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.89).', NULL, NULL, '2026-09-16T08:12:08'),
 (104, 36, NULL, 'AI_CLASSIFIED', N'AI phân loại: Xả thải (độ tin cậy 0.88).', NULL, NULL, '2026-10-11T07:48:08'),
 (105, 16, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.92).', NULL, NULL, '2026-10-08T08:12:08'),
 (106, 24, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.92).', NULL, NULL, '2026-10-10T17:12:08'),
 (107, 35, NULL, 'AI_CLASSIFIED', N'AI không chắc chắn (nhãn infrastructure, độ tin cậy 0.52). Chuyển điều phối viên.', NULL, NULL, '2026-10-11T07:36:08'),
 (108, 3, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.91).', NULL, NULL, '2026-09-18T20:12:08'),
 (109, 26, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.89).', NULL, NULL, '2026-10-10T22:12:08'),
 (110, 37, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.92).', NULL, NULL, '2026-09-25T13:12:08'),
 (111, 6, NULL, 'AI_CLASSIFIED', N'AI phân loại: Hư hỏng hạ tầng (độ tin cậy 0.94).', NULL, NULL, '2026-09-25T12:12:08'),
 (112, 25, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.88).', NULL, NULL, '2026-10-10T20:12:08'),
 (113, 21, NULL, 'AI_CLASSIFIED', N'AI phân loại: Xả thải (độ tin cậy 0.87).', NULL, NULL, '2026-10-10T02:12:08'),
 (114, 17, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.88).', NULL, NULL, '2026-10-08T20:12:08'),
 (115, 5, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.90).', NULL, NULL, '2026-09-23T10:12:08'),
 (116, 23, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.91).', NULL, NULL, '2026-10-10T12:12:08'),
 (117, 39, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.90).', NULL, NULL, '2026-10-10T13:12:08'),
 (118, 9, NULL, 'AI_CLASSIFIED', N'AI phân loại: Rác thải (độ tin cậy 0.92).', NULL, NULL, '2026-10-02T04:12:08'),
 (119, 19, NULL, 'AI_CLASSIFIED', N'AI phân loại: Lấn chiếm vỉa hè (độ tin cậy 0.87).', NULL, NULL, '2026-10-09T11:12:08'),
 (120, 31, NULL, 'AI_CLASSIFIED', N'AI phân loại: Xả thải (độ tin cậy 0.86).', NULL, NULL, '2026-10-11T04:36:08'),
 (121, 38, NULL, 'MERGED', N'Gộp vào phản ánh DN-2026-E4RM cùng vị trí.', NULL, NULL, '2026-10-10T07:12:09'),
 (122, 40, NULL, 'MERGED', N'Gộp vào phản ánh DN-2026-L3TN cùng vị trí.', NULL, NULL, '2026-10-11T02:42:09'),
 (123, 37, NULL, 'MERGED', N'Gộp vào phản ánh DN-2026-T9LF cùng vị trí.', NULL, NULL, '2026-09-25T13:12:09'),
 (124, 39, NULL, 'MERGED', N'Gộp vào phản ánh DN-2026-X6PL cùng vị trí.', NULL, NULL, '2026-10-10T13:12:09'),
 (125, 22, NULL, 'MERGED', N'Nhận thêm phản ánh trùng DN-2026-E5RN. Tổng 2 phản ánh.', NULL, NULL, '2026-10-10T07:12:09'),
 (126, 28, NULL, 'MERGED', N'Nhận thêm phản ánh trùng DN-2026-L4TP. Tổng 2 phản ánh.', NULL, NULL, '2026-10-11T02:42:09'),
 (127, 6, NULL, 'MERGED', N'Nhận thêm phản ánh trùng DN-2026-T2LG. Tổng 2 phản ánh.', NULL, NULL, '2026-09-25T13:12:09'),
 (128, 23, NULL, 'MERGED', N'Nhận thêm phản ánh trùng DN-2026-X7PM. Tổng 2 phản ánh.', NULL, NULL, '2026-10-10T13:12:09'),
 (129, 29, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Phan Thị Cán Bộ 10. Hạn xử lý 24 giờ.', 0, 1, '2026-10-11T03:14:00'),
 (130, 8, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Dương Văn Cán Bộ 09. Hạn xử lý 4 giờ.', 0, 1, '2026-09-29T16:14:00'),
 (131, 14, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Lý Thị Cán Bộ 08. Hạn xử lý 4 giờ.', 0, 1, '2026-10-06T18:14:00'),
 (132, 13, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Hoàng Thị Cán Bộ 02. Hạn xử lý 24 giờ.', 0, 1, '2026-10-06T08:14:00'),
 (133, 10, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Trương Văn Cán Bộ 07. Hạn xử lý 48 giờ.', 0, 1, '2026-10-03T00:14:00'),
 (134, 32, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Ngô Thị Cán Bộ 06. Hạn xử lý 4 giờ.', 0, 1, '2026-10-11T06:14:00'),
 (135, 22, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Hoàng Thị Cán Bộ 02. Hạn xử lý 24 giờ.', 0, 1, '2026-10-10T06:14:00'),
 (136, 15, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Võ Văn Cán Bộ 03. Hạn xử lý 48 giờ.', 0, 1, '2026-10-07T08:14:00'),
 (137, 12, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Bùi Văn Cán Bộ 05. Hạn xử lý 24 giờ.', 0, 1, '2026-10-05T12:14:00'),
 (138, 20, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Bùi Văn Cán Bộ 05. Hạn xử lý 48 giờ.', 0, 1, '2026-10-10T02:14:00'),
 (139, 4, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Trương Văn Cán Bộ 07. Hạn xử lý 4 giờ.', 0, 1, '2026-09-21T08:14:00'),
 (140, 18, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Trương Văn Cán Bộ 07. Hạn xử lý 4 giờ.', 0, 1, '2026-10-09T06:14:00'),
 (141, 1, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Hoàng Thị Cán Bộ 02. Hạn xử lý 24 giờ.', 0, 1, '2026-09-14T06:14:00'),
 (142, 28, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Đặng Thị Cán Bộ 04. Hạn xử lý 4 giờ.', 0, 1, '2026-10-11T02:14:00'),
 (143, 7, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Phạm Minh Cán Bộ 01. Hạn xử lý 48 giờ.', 0, 1, '2026-09-27T14:14:00'),
 (144, 27, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Ngô Thị Cán Bộ 06. Hạn xử lý 4 giờ.', 0, 1, '2026-10-11T00:14:00'),
 (145, 11, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Ngô Thị Cán Bộ 06. Hạn xử lý 4 giờ.', 0, 1, '2026-10-04T06:14:00'),
 (146, 2, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Võ Văn Cán Bộ 03. Hạn xử lý 4 giờ.', 0, 1, '2026-09-16T08:14:00'),
 (147, 16, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Bùi Văn Cán Bộ 05. Hạn xử lý 24 giờ.', 0, 1, '2026-10-08T08:14:00'),
 (148, 24, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Phạm Minh Cán Bộ 01. Hạn xử lý 24 giờ.', 0, 1, '2026-10-10T17:14:00'),
 (149, 3, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Bùi Văn Cán Bộ 05. Hạn xử lý 48 giờ.', 0, 1, '2026-09-18T20:14:00'),
 (150, 26, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Lý Thị Cán Bộ 08. Hạn xử lý 24 giờ.', 0, 1, '2026-10-10T22:14:00'),
 (151, 6, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Lâm Thị Cán Bộ 12. Hạn xử lý 4 giờ.', 0, 1, '2026-09-25T12:14:00'),
 (152, 25, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Lâm Thị Cán Bộ 12. Hạn xử lý 48 giờ.', 0, 1, '2026-10-10T20:14:00'),
 (153, 21, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Đặng Thị Cán Bộ 04. Hạn xử lý 4 giờ.', 0, 1, '2026-10-10T02:14:00'),
 (154, 17, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Võ Văn Cán Bộ 03. Hạn xử lý 24 giờ.', 0, 1, '2026-10-08T20:14:00'),
 (155, 5, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Phan Thị Cán Bộ 10. Hạn xử lý 24 giờ.', 0, 1, '2026-09-23T10:14:00'),
 (156, 23, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Hoàng Thị Cán Bộ 02. Hạn xử lý 24 giờ.', 0, 1, '2026-10-10T12:14:00'),
 (157, 9, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Phan Thị Cán Bộ 10. Hạn xử lý 24 giờ.', 0, 1, '2026-10-02T04:14:00'),
 (158, 19, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Huỳnh Văn Cán Bộ 11. Hạn xử lý 48 giờ.', 0, 1, '2026-10-09T11:14:00'),
 (159, 31, NULL, 'DISPATCHED', N'Tự động giao cho cán bộ Phạm Minh Cán Bộ 01. Hạn xử lý 4 giờ.', 0, 1, '2026-10-11T04:38:00'),
 (160, 22, NULL, 'ESCALATED', N'Quá mức cảnh báo thời hạn chưa nhận việc. Leo thang tới: Lê Văn Lãnh Đạo TB.', NULL, NULL, '2026-10-11T01:26:00'),
 (161, 11, NULL, 'ESCALATED', N'Quá mức cảnh báo thời hạn chưa nhận việc. Leo thang tới: Đỗ Văn Lãnh Đạo TH.', NULL, NULL, '2026-10-04T09:26:00'),
 (162, 31, NULL, 'ESCALATED', N'Quá mức cảnh báo thời hạn chưa nhận việc. Leo thang tới: Lê Văn Lãnh Đạo TB.', NULL, NULL, '2026-10-11T07:50:00'),
 (163, 8, 17, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-29T16:34:00'),
 (164, 14, 16, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-06T18:26:00'),
 (165, 13, 8, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-06T08:59:00'),
 (166, 15, 9, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-07T12:14:00'),
 (167, 12, 11, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-05T12:44:00'),
 (168, 20, 11, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-10T04:14:00'),
 (169, 4, 15, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-21T08:44:00'),
 (170, 18, 15, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-09T06:24:00'),
 (171, 1, 8, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-14T06:39:00'),
 (172, 28, 10, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-11T02:34:00'),
 (173, 7, 7, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-27T19:14:00'),
 (174, 27, 12, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-11T00:29:00'),
 (175, 11, 12, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-04T09:34:00'),
 (176, 2, 9, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-16T08:54:00'),
 (177, 16, 11, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-08T08:49:00'),
 (178, 24, 7, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-10T17:44:00'),
 (179, 3, 11, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-18T23:14:00'),
 (180, 26, 16, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-11T01:14:00'),
 (181, 6, 20, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-25T12:29:00'),
 (182, 21, 10, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-10T02:39:00'),
 (183, 5, 18, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-09-23T11:14:00'),
 (184, 23, 8, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-10T13:04:00'),
 (185, 9, 18, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-02T05:44:00'),
 (186, 19, 19, 'ACCEPTED', N'Cán bộ nhận việc.', 1, 2, '2026-10-09T13:44:00'),
 (187, 22, NULL, 'OVERDUE', N'Sự cố đã quá hạn xử lý theo SLA.', NULL, NULL, '2026-10-11T06:14:00'),
 (188, 4, NULL, 'OVERDUE', N'Sự cố đã quá hạn xử lý theo SLA.', NULL, NULL, '2026-09-21T12:14:00'),
 (189, 28, NULL, 'OVERDUE', N'Sự cố đã quá hạn xử lý theo SLA.', NULL, NULL, '2026-10-11T06:14:00'),
 (190, 9, NULL, 'OVERDUE', N'Sự cố đã quá hạn xử lý theo SLA.', NULL, NULL, '2026-10-03T04:14:00'),
 (191, 8, 17, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-29T20:04:00'),
 (192, 14, 16, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-06T21:26:00'),
 (193, 13, 8, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-06T13:59:00'),
 (194, 15, 9, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-08T14:14:00'),
 (195, 12, 11, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-05T16:44:00'),
 (196, 4, 15, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-21T14:14:00'),
 (197, 18, 15, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-09T08:12:00'),
 (198, 1, 8, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-14T12:39:00'),
 (199, 7, 7, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-29T01:14:00'),
 (200, 27, 12, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-11T02:41:00'),
 (201, 11, 12, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-04T10:04:00'),
 (202, 2, 9, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-16T12:06:00'),
 (203, 16, 11, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-08T16:49:00'),
 (204, 24, 7, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-10T20:44:00'),
 (205, 3, 11, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-19T19:14:00'),
 (206, 6, 20, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-25T14:59:00'),
 (207, 21, 10, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-10T05:39:00'),
 (208, 5, 18, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-09-24T05:14:00'),
 (209, 9, 18, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-03T11:44:00'),
 (210, 19, 19, 'RESOLVED', N'Đã xử lý xong, có ảnh nghiệm thu.', 2, 3, '2026-10-10T11:44:00'),
 (211, 37, NULL, 'RESOLVED', N'Đóng theo phản ánh gốc DN-2026-T9LF.', 0, 3, '2026-09-25T14:59:00'),
 (212, 10, 3, 'CANCELLED', N'Hủy: Tranh chấp lối đi giữa hai hộ dân, không thuộc phạm vi trật tự đô thị. Đã hướng dẫn liên hệ tổ hòa giải khu phố.', 1, 4, '2026-10-03T01:49:00'),
 (213, 17, 3, 'CANCELLED', N'Hủy: Người phản ánh xác nhận rác đã được đội thu gom xử lý trước khi cán bộ đến.', 1, 4, '2026-10-08T21:49:00'),
 (214, 31, NULL, 'OVERDUE', N'Sự cố đã quá hạn xử lý theo SLA.', NULL, NULL, '2026-10-11T08:38:47'),
 (215, 32, NULL, 'ESCALATED', N'Quá mức cảnh báo thời hạn chưa nhận việc. Leo thang tới: Đỗ Văn Lãnh Đạo TH.', NULL, NULL, '2026-10-11T09:26:51'),
 (216, 32, NULL, 'OVERDUE', N'Sự cố đã quá hạn xử lý theo SLA.', NULL, NULL, '2026-10-11T10:14:55'),
 (217, 23, NULL, 'OVERDUE', N'Sự cố đã quá hạn xử lý theo SLA.', NULL, NULL, '2026-10-11T12:14:42');
SET IDENTITY_INSERT dbo.tbl_incident_logs OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_incident_ratings (16 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_incident_ratings ON;
INSERT INTO dbo.tbl_incident_ratings ([id], [incident_id], [rating], [feedback], [created_at]) VALUES
 (1, 8, 4, N'Đã kiểm tra và yêu cầu cơ sở dừng xả.', '2026-09-29T22:04:00'),
 (2, 14, 5, N'Cán bộ đến rào chắn ngay, hôm sau đã hàn lại.', '2026-10-06T23:26:00'),
 (3, 13, 5, NULL, '2026-10-06T15:59:00'),
 (4, 15, 3, N'Hôm sau lại tái diễn, cần kiểm tra thường xuyên.', '2026-10-08T16:14:00'),
 (5, 12, 4, NULL, '2026-10-05T18:44:00'),
 (6, 4, 3, N'Xử lý được nhưng hơi chậm so với cam kết.', '2026-09-21T16:14:00'),
 (7, 18, 5, N'Rất nhanh!', '2026-10-09T10:12:00'),
 (8, 1, 5, N'Xử lý nhanh, khu chợ sạch sẽ trở lại.', '2026-09-14T14:39:00'),
 (9, 27, 4, NULL, '2026-10-11T04:41:00'),
 (10, 11, 4, NULL, '2026-10-04T12:04:00'),
 (11, 2, 4, N'Đã vá nhưng mặt đường còn hơi gồ ghề.', '2026-09-16T14:06:00'),
 (12, 3, 4, NULL, '2026-09-19T21:14:00'),
 (13, 6, 5, N'Đèn sáng lại ngay trong đêm.', '2026-09-25T16:59:00'),
 (14, 5, 5, N'Cảm ơn cán bộ khu phố.', '2026-09-24T07:14:00'),
 (15, 9, 2, N'Phải phản ánh lần hai mới được xử lý.', '2026-10-03T13:44:00'),
 (16, 19, 4, NULL, '2026-10-10T13:44:00');
SET IDENTITY_INSERT dbo.tbl_incident_ratings OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* ---------- tbl_notifications (55 dong) ---------- */
SET IDENTITY_INSERT dbo.tbl_notifications ON;
INSERT INTO dbo.tbl_notifications ([id], [user_id], [incident_id], [notification_type], [title], [content], [is_read], [created_at], [read_at]) VALUES
 (1, 8, 1, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-K7QM', N'Đường Cách Mạng Tháng Tám, gần chợ Biên Hòa, phường Trấn Biên', 1, '2026-09-14T06:14:00', '2026-09-14T06:44:00'),
 (2, 9, 2, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-P3XT', N'Quốc lộ 1K, đoạn qua khu vực Hóa An, phường Biên Hòa', 1, '2026-09-16T08:14:00', '2026-09-16T08:44:00'),
 (3, 11, 3, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-R8VD', N'Đường Phạm Văn Thuận, khu vực Tân Mai, phường Tam Hiệp', 1, '2026-09-18T20:14:00', '2026-09-18T20:44:00'),
 (4, 15, 4, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-H2NB', N'Khu vực Cù Lao Phố (Hiệp Hòa cũ), phường Trấn Biên', 1, '2026-09-21T08:14:00', '2026-09-21T08:44:00'),
 (5, 4, 4, 'OVERDUE', N'Quá hạn: DN-2026-H2NB', N'Sự cố đã quá thời hạn xử lý cam kết.', 1, '2026-09-21T12:14:00', '2026-09-21T12:44:00'),
 (6, 15, 4, 'OVERDUE', N'Quá hạn: DN-2026-H2NB', N'Sự cố đã quá thời hạn xử lý cam kết.', 1, '2026-09-21T12:14:00', '2026-09-21T12:44:00'),
 (7, 18, 5, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-W5CK', N'Đường Bùi Hữu Nghĩa, khu vực Tân Vạn, phường Biên Hòa', 1, '2026-09-23T10:14:00', '2026-09-23T10:44:00'),
 (8, 20, 6, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-T9LF', N'Gần vòng xoay Tam Hiệp, phường Tam Hiệp', 1, '2026-09-25T12:14:00', '2026-09-25T12:44:00'),
 (9, 7, 7, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-M4GZ', N'Đường Huỳnh Văn Nghệ, khu vực Bửu Long, phường Trấn Biên', 1, '2026-09-27T14:14:00', '2026-09-27T14:44:00'),
 (10, 17, 8, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-B6SJ', N'Khu công nghiệp Biên Hòa 1, khu vực An Bình, phường Trấn Biên', 1, '2026-09-29T16:14:00', '2026-09-29T16:44:00'),
 (11, 18, 9, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-Y3HP', N'Khu vực Bửu Hòa, phường Biên Hòa', 1, '2026-10-02T04:14:00', '2026-10-02T04:44:00'),
 (12, 15, 10, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-C8YZ', N'Đường Hà Huy Giáp, phường Trấn Biên', 1, '2026-10-03T00:14:00', '2026-10-03T00:44:00'),
 (13, 5, 9, 'OVERDUE', N'Quá hạn: DN-2026-Y3HP', N'Sự cố đã quá thời hạn xử lý cam kết.', 1, '2026-10-03T04:14:00', '2026-10-03T04:44:00'),
 (14, 18, 9, 'OVERDUE', N'Quá hạn: DN-2026-Y3HP', N'Sự cố đã quá thời hạn xử lý cam kết.', 1, '2026-10-03T04:14:00', '2026-10-03T04:44:00'),
 (15, 12, 11, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-N8RA', N'Gần chợ Bình Đa, phường Tam Hiệp', 1, '2026-10-04T06:14:00', '2026-10-04T06:44:00'),
 (16, 6, 11, 'ESCALATION', N'Cảnh báo SLA: DN-2026-N8RA', N'Sự cố chưa được nhận việc, hạn xử lý 2026-10-04T10:14:00Z.', 1, '2026-10-04T09:26:00', '2026-10-04T09:56:00'),
 (17, 11, 12, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-G3VU', N'Khu vực Tân Hiệp, phường Tam Hiệp', 1, '2026-10-05T12:14:00', '2026-10-05T12:44:00'),
 (18, 8, 13, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-C5UE', N'Đường Phan Đình Phùng, khu vực Quang Vinh, phường Trấn Biên', 1, '2026-10-06T08:14:00', '2026-10-06T08:44:00'),
 (19, 16, 14, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-B8NH', N'Khu vực Thống Nhất, phường Trấn Biên', 1, '2026-10-06T18:14:00', '2026-10-06T18:44:00'),
 (20, 9, 15, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-F7KX', N'Khu vực Tân Hạnh, phường Biên Hòa', 1, '2026-10-07T08:14:00', '2026-10-07T08:44:00'),
 (21, 11, 16, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-Q2WD', N'Đường Đồng Khởi, khu vực Tân Hiệp, phường Tam Hiệp', 1, '2026-10-08T08:14:00', '2026-10-08T08:44:00'),
 (22, 9, 17, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-W2JA', N'Khu vực Hóa An, phường Biên Hòa', 1, '2026-10-08T20:14:00', '2026-10-08T20:44:00'),
 (23, 15, 18, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-J9ZT', N'Đường Võ Thị Sáu, khu vực Thống Nhất, phường Trấn Biên', 1, '2026-10-09T06:14:00', '2026-10-09T06:44:00'),
 (24, 19, 19, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-Y6KD', N'Khu vực Tân Vạn, giáp Dĩ An, phường Biên Hòa', 1, '2026-10-09T11:14:00', '2026-10-09T11:44:00'),
 (25, 10, 21, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-V4MB', N'Đường Nguyễn Tri Phương, khu vực Bửu Hòa, phường Biên Hòa', 1, '2026-10-10T02:14:00', '2026-10-10T02:44:00'),
 (26, 11, 20, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-G8DS', N'Đường Phan Trung, khu vực Tân Mai, phường Tam Hiệp', 1, '2026-10-10T02:14:00', '2026-10-10T02:44:00'),
 (27, 8, 22, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-E4RM', N'Đường 30 Tháng 4, khu vực Trung Dũng, phường Trấn Biên', 1, '2026-10-10T06:14:00', '2026-10-10T06:44:00'),
 (28, 8, 23, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-X6PL', N'Đường Nguyễn Ái Quốc, khu vực Trung Dũng, phường Trấn Biên', 0, '2026-10-10T12:14:00', NULL),
 (29, 7, 24, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-Q5HS', N'Khu vực Bửu Long, phường Trấn Biên', 0, '2026-10-10T17:14:00', NULL),
 (30, 20, 25, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-U8BL', N'Khu vực Bình Đa, phường Tam Hiệp', 0, '2026-10-10T20:14:00', NULL),
 (31, 16, 26, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-S5HV', N'Đường Trần Quốc Toản, khu vực An Bình, phường Trấn Biên', 0, '2026-10-10T22:14:00', NULL),
 (32, 12, 27, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-N2CT', N'Khu vực Tam Hiệp, phường Tam Hiệp', 0, '2026-10-11T00:14:00', NULL),
 (33, 4, 22, 'ESCALATION', N'Cảnh báo SLA: DN-2026-E4RM', N'Sự cố chưa được nhận việc, hạn xử lý 2026-10-11T06:14:00Z.', 0, '2026-10-11T01:26:00', NULL),
 (34, 10, 28, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-L3TN', N'Gần chân cầu Hóa An, phường Biên Hòa', 0, '2026-10-11T02:14:00', NULL),
 (35, 18, 29, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-A9FW', N'Khu vực Tân Vạn, phường Biên Hòa', 0, '2026-10-11T03:14:00', NULL),
 (36, 3, 30, 'MANUAL_REVIEW', N'Cần duyệt: DN-2026-K8TD', N'Sự cố nằm ngoài các ô lưới, cần chọn đơn vị phụ trách.', 0, '2026-10-11T04:12:10', NULL),
 (37, 7, 31, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-Z7KC', N'Đường Huỳnh Văn Nghệ, gần khu du lịch Bửu Long, phường Trấn Biên', 0, '2026-10-11T04:38:00', NULL),
 (38, 12, 32, 'NEW_ASSIGNMENT', N'Việc mới: DN-2026-D2QX', N'Đường Phạm Văn Thuận, gần chợ Tam Hiệp, phường Tam Hiệp', 0, '2026-10-11T06:14:00', NULL),
 (39, 4, 22, 'OVERDUE', N'Quá hạn: DN-2026-E4RM', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T06:14:00', NULL),
 (40, 8, 22, 'OVERDUE', N'Quá hạn: DN-2026-E4RM', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T06:14:00', NULL),
 (41, 5, 28, 'OVERDUE', N'Quá hạn: DN-2026-L3TN', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T06:14:00', NULL),
 (42, 10, 28, 'OVERDUE', N'Quá hạn: DN-2026-L3TN', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T06:14:00', NULL),
 (43, 3, 33, 'MANUAL_REVIEW', N'Cần duyệt: DN-2026-H5NC', N'Sự cố nằm ngoài các ô lưới, cần chọn đơn vị phụ trách.', 0, '2026-10-11T06:42:10', NULL),
 (44, 3, 34, 'MANUAL_REVIEW', N'Cần duyệt: DN-2026-M6QS', N'AI không nhận diện được loại sự cố, cần phân loại thủ công.', 0, '2026-10-11T07:24:10', NULL),
 (45, 4, 34, 'MANUAL_REVIEW', N'Cần duyệt: DN-2026-M6QS', N'AI không nhận diện được loại sự cố, cần phân loại thủ công.', 0, '2026-10-11T07:24:10', NULL),
 (46, 3, 35, 'MANUAL_REVIEW', N'Cần duyệt: DN-2026-R3BX', N'AI không nhận diện được loại sự cố, cần phân loại thủ công.', 0, '2026-10-11T07:36:10', NULL),
 (47, 3, 36, 'MANUAL_REVIEW', N'Cần duyệt: DN-2026-P9WE', N'Sự cố nằm ngoài các ô lưới, cần chọn đơn vị phụ trách.', 0, '2026-10-11T07:48:10', NULL),
 (48, 4, 31, 'ESCALATION', N'Cảnh báo SLA: DN-2026-Z7KC', N'Sự cố chưa được nhận việc, hạn xử lý 2026-10-11T08:38:00Z.', 0, '2026-10-11T07:50:00', NULL),
 (49, 4, 31, 'OVERDUE', N'Quá hạn: DN-2026-Z7KC', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T08:38:47', NULL),
 (50, 7, 31, 'OVERDUE', N'Quá hạn: DN-2026-Z7KC', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T08:38:47', NULL),
 (51, 6, 32, 'ESCALATION', N'Cảnh báo SLA: DN-2026-D2QX', N'Sự cố chưa được nhận việc, hạn xử lý 2026-10-11T10:14:00Z.', 0, '2026-10-11T09:26:51', NULL),
 (52, 6, 32, 'OVERDUE', N'Quá hạn: DN-2026-D2QX', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T10:14:55', NULL),
 (53, 12, 32, 'OVERDUE', N'Quá hạn: DN-2026-D2QX', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T10:14:55', NULL),
 (54, 4, 23, 'OVERDUE', N'Quá hạn: DN-2026-X6PL', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T12:14:42', NULL),
 (55, 8, 23, 'OVERDUE', N'Quá hạn: DN-2026-X6PL', N'Sự cố đã quá thời hạn xử lý cam kết.', 0, '2026-10-11T12:14:42', NULL);
SET IDENTITY_INSERT dbo.tbl_notifications OFF;
GO
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
/* Doi moi cot thoi gian them (luc chay - luc xuat 2026-10-13T08:59:13 UTC). Bo qua moc 9999 (khoa vinh vien). */
DECLARE @shift INT = DATEDIFF(SECOND, '2026-10-13T08:59:13', SYSUTCDATETIME());
UPDATE dbo.tbl_admin_units SET
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END,
    [updated_at] = CASE WHEN [updated_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [updated_at]) ELSE [updated_at] END;
UPDATE dbo.tbl_roles SET
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END;
UPDATE dbo.tbl_users SET
    [locked_until] = CASE WHEN [locked_until] < '9000-01-01' THEN DATEADD(SECOND, @shift, [locked_until]) ELSE [locked_until] END,
    [last_login_at] = CASE WHEN [last_login_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [last_login_at]) ELSE [last_login_at] END,
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END,
    [updated_at] = CASE WHEN [updated_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [updated_at]) ELSE [updated_at] END;
UPDATE dbo.tbl_user_roles SET
    [assigned_at] = CASE WHEN [assigned_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [assigned_at]) ELSE [assigned_at] END,
    [expires_at] = CASE WHEN [expires_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [expires_at]) ELSE [expires_at] END;
UPDATE dbo.tbl_grids SET
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END,
    [updated_at] = CASE WHEN [updated_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [updated_at]) ELSE [updated_at] END;
UPDATE dbo.tbl_grid_officers SET
    [assigned_at] = CASE WHEN [assigned_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [assigned_at]) ELSE [assigned_at] END;
UPDATE dbo.tbl_incidents SET
    [sla_deadline] = CASE WHEN [sla_deadline] < '9000-01-01' THEN DATEADD(SECOND, @shift, [sla_deadline]) ELSE [sla_deadline] END,
    [accepted_at] = CASE WHEN [accepted_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [accepted_at]) ELSE [accepted_at] END,
    [escalated_at] = CASE WHEN [escalated_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [escalated_at]) ELSE [escalated_at] END,
    [resolved_at] = CASE WHEN [resolved_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [resolved_at]) ELSE [resolved_at] END,
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END,
    [updated_at] = CASE WHEN [updated_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [updated_at]) ELSE [updated_at] END,
    [processed_at] = CASE WHEN [processed_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [processed_at]) ELSE [processed_at] END;
UPDATE dbo.tbl_incident_images SET
    [uploaded_at] = CASE WHEN [uploaded_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [uploaded_at]) ELSE [uploaded_at] END;
UPDATE dbo.tbl_incident_logs SET
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END;
UPDATE dbo.tbl_incident_ratings SET
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END;
UPDATE dbo.tbl_notifications SET
    [created_at] = CASE WHEN [created_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [created_at]) ELSE [created_at] END,
    [read_at] = CASE WHEN [read_at] < '9000-01-01' THEN DATEADD(SECOND, @shift, [read_at]) ELSE [read_at] END;
GO

/* =====================================================================
   Kiem tra va COMMIT
   ===================================================================== */
IF @@TRANCOUNT = 0 SET NOEXEC ON;
GO
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) < 18
   OR (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped = 0) < 143
   OR (SELECT COUNT(*) FROM sys.objects WHERE type IN ('FN', 'IF', 'TF') AND is_ms_shipped = 0) < 5
   OR (SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped = 0) < 2
   OR OBJECT_ID(N'dbo.trg_tbl_admin_units_path', N'TR') IS NULL
    THROW 50002, N'Thiếu bảng/proc/function/view/trigger, đã rollback.', 1;

IF EXISTS (SELECT 1 FROM (
    SELECT 'tbl_admin_levels', 3, (SELECT COUNT(*) FROM dbo.tbl_admin_levels) UNION ALL
    SELECT 'tbl_admin_unit_types', 9, (SELECT COUNT(*) FROM dbo.tbl_admin_unit_types) UNION ALL
    SELECT 'tbl_admin_units', 48, (SELECT COUNT(*) FROM dbo.tbl_admin_units) UNION ALL
    SELECT 'tbl_roles', 5, (SELECT COUNT(*) FROM dbo.tbl_roles) UNION ALL
    SELECT 'tbl_permissions', 26, (SELECT COUNT(*) FROM dbo.tbl_permissions) UNION ALL
    SELECT 'tbl_role_permissions', 53, (SELECT COUNT(*) FROM dbo.tbl_role_permissions) UNION ALL
    SELECT 'tbl_users', 19, (SELECT COUNT(*) FROM dbo.tbl_users) UNION ALL
    SELECT 'tbl_user_roles', 46, (SELECT COUNT(*) FROM dbo.tbl_user_roles) UNION ALL
    SELECT 'tbl_categories', 5, (SELECT COUNT(*) FROM dbo.tbl_categories) UNION ALL
    SELECT 'tbl_sla_policies', 3, (SELECT COUNT(*) FROM dbo.tbl_sla_policies) UNION ALL
    SELECT 'tbl_grids', 54, (SELECT COUNT(*) FROM dbo.tbl_grids) UNION ALL
    SELECT 'tbl_grid_officers', 54, (SELECT COUNT(*) FROM dbo.tbl_grid_officers) UNION ALL
    SELECT 'tbl_incidents', 40, (SELECT COUNT(*) FROM dbo.tbl_incidents) UNION ALL
    SELECT 'tbl_incident_images', 60, (SELECT COUNT(*) FROM dbo.tbl_incident_images) UNION ALL
    SELECT 'tbl_incident_logs', 217, (SELECT COUNT(*) FROM dbo.tbl_incident_logs) UNION ALL
    SELECT 'tbl_incident_ratings', 16, (SELECT COUNT(*) FROM dbo.tbl_incident_ratings) UNION ALL
    SELECT 'tbl_notifications', 55, (SELECT COUNT(*) FROM dbo.tbl_notifications)
) c (bang, mong_doi, thuc_te) WHERE mong_doi <> thuc_te)
    THROW 50003, N'Số dòng dữ liệu không khớp bản xuất, đã rollback.', 1;

COMMIT TRANSACTION;
EXEC sys.sp_set_session_context @key = N'gridsmart_dev_migration', @value = 1;
GO

SET NOEXEC OFF;
GO
IF SESSION_CONTEXT(N'gridsmart_dev_migration') IS NULL
    RAISERROR (N'MIGRATION KHÔNG HOÀN TẤT. Xem lỗi phía trên; mọi thay đổi trong giao dịch đã được rollback.', 16, 1);
ELSE
BEGIN
    PRINT N'================ MIGRATION HOÀN TẤT ================';
    PRINT N'Mật khẩu mọi tài khoản: GridSmart@2026';
    SELECT u.username, STRING_AGG(r.role_code + N' @ ' + ISNULL(a.name, N'toàn hệ thống'), N'; ') AS vai_tro
    FROM dbo.tbl_users u
    JOIN dbo.tbl_user_roles ur ON ur.user_id = u.id AND ur.is_active = 1
    JOIN dbo.tbl_roles r ON r.id = ur.role_id
    LEFT JOIN dbo.tbl_admin_units a ON a.id = ur.admin_unit_id
    WHERE u.username IS NOT NULL AND r.role_code <> 'GRID_OFFICER'
    GROUP BY u.id, u.username
    UNION ALL
    SELECT u.username, N'GRID_OFFICER @ ' + CAST(COUNT(*) AS NVARCHAR(10)) + N' khu phố'
    FROM dbo.tbl_users u
    JOIN dbo.tbl_user_roles ur ON ur.user_id = u.id AND ur.is_active = 1
    JOIN dbo.tbl_roles r ON r.id = ur.role_id AND r.role_code = 'GRID_OFFICER'
    GROUP BY u.id, u.username
    ORDER BY 1;
END;
GO