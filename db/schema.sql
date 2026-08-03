-- =====================================================================
--  Alfar Quest — MySQL schema
--  Table/column names match the EF Core model so the API works whether
--  you run this script OR let AlfarQuest.Api create the schema itself.
--  Run:  mysql -u root -p < db/schema.sql
-- =====================================================================

CREATE DATABASE IF NOT EXISTS alfar_quest
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE alfar_quest;

CREATE TABLE IF NOT EXISTS Heroes (
  `Key`               VARCHAR(40)  NOT NULL,
  Name                VARCHAR(120) NOT NULL,
  Title               VARCHAR(160) NOT NULL,
  HeroClass           VARCHAR(40)  NOT NULL,
  Description         TEXT         NOT NULL,
  BaseHp              INT          NOT NULL,
  UnlockedByDefault   TINYINT(1)   NOT NULL DEFAULT 1,
  PRIMARY KEY (`Key`)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Saves (
  Id                INT           NOT NULL AUTO_INCREMENT,
  PlayerName        VARCHAR(80)   NOT NULL,
  ActiveHeroKey     VARCHAR(40)   NOT NULL DEFAULT 'mage',
  Region            VARCHAR(60)   NOT NULL DEFAULT 'cave_beyond_time',
  PosX              FLOAT         NOT NULL DEFAULT 0,
  PosY              FLOAT         NOT NULL DEFAULT 0,
  PlaytimeSeconds   BIGINT        NOT NULL DEFAULT 0,
  UpdatedAt         DATETIME(6)   NOT NULL,
  PRIMARY KEY (Id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS SaveHeroes (
  Id            INT          NOT NULL AUTO_INCREMENT,
  PlayerSaveId  INT          NOT NULL,
  HeroKey       VARCHAR(40)  NOT NULL,
  Level         INT          NOT NULL DEFAULT 1,
  Xp            INT          NOT NULL DEFAULT 0,
  Recruited     TINYINT(1)   NOT NULL DEFAULT 0,
  PRIMARY KEY (Id),
  KEY IX_SaveHeroes_PlayerSaveId (PlayerSaveId),
  CONSTRAINT FK_SaveHeroes_Saves FOREIGN KEY (PlayerSaveId)
    REFERENCES Saves (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- The three canonical delvers from "Story for Music".
INSERT INTO Heroes (`Key`, Name, Title, HeroClass, Description, BaseHp, UnlockedByDefault) VALUES
 ('mage',   'The Fallen Mage',   'Bearer of the Caged Fire',       'Mage',   'An exile of Kae Ychel with a greater demon bound inside his heart. Hurls discs of light — and, when pressed, unleashes the hellfire he can barely contain.', 90,  1),
 ('cleric', 'The Grieving Cleric','Whose Faith Fractured',          'Cleric', 'A priest who traded his own descent into madness for a phial of panacea. Wades in with blessed plate and gilded mace, and can loose a nova of holy light.', 140, 0),
 ('thief',  'The Hollow Thief',  'Whose Crew the Crystal Took',     'Thief',  'A Seoshe gambler with a shard of the stolen crystal fused into his arm. Fires a crossbow from the dark and dashes through danger with reckless luck.', 100, 1)
-- UnlockedByDefault is part of the upsert so re-running this script heals a
-- database seeded before the Cleric was locked (he joins at the Cave, not at
-- the select screen — see Game/Lore.cs).
ON DUPLICATE KEY UPDATE Name = VALUES(Name), UnlockedByDefault = VALUES(UnlockedByDefault);
