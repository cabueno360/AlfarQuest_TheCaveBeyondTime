-- GENERATED from the EF model — do not edit by hand.
-- Regenerate after any entity change:
--     dotnet run --project src/AlfarQuest.Api -- --dump-schema

CREATE TABLE `Accounts` (
    `Id` char(36) NOT NULL,
    `Email` varchar(254) NOT NULL,
    `PasswordHash` varchar(256) NOT NULL,
    `FullName` varchar(80) NOT NULL,
    `Country` varchar(56) NULL,
    `PhoneNumber` varchar(32) NULL,
    `RegisteredAt` datetime(6) NOT NULL,
    `LastLoginAt` datetime(6) NULL,
    `AvatarVersion` varchar(64) NULL,
    `CurrentCharacter` varchar(40) NULL,
    `HighestLevel` int NOT NULL,
    `TotalGold` bigint NOT NULL,
    `TotalPlayTimeSeconds` bigint NOT NULL,
    `EmailConfirmed` tinyint(1) NOT NULL,
    `ExternalProvider` varchar(40) NULL,
    `ExternalSubjectId` varchar(128) NULL,
    PRIMARY KEY (`Id`)
);


CREATE TABLE `Heroes` (
    `Key` varchar(40) NOT NULL,
    `Name` longtext NOT NULL,
    `Title` longtext NOT NULL,
    `HeroClass` longtext NOT NULL,
    `Description` longtext NOT NULL,
    `BaseHp` int NOT NULL,
    `UnlockedByDefault` tinyint(1) NOT NULL,
    PRIMARY KEY (`Key`)
);


CREATE TABLE `Saves` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerAccountId` char(36) NOT NULL,
    `PlayerName` longtext NOT NULL,
    `ActiveHeroKey` longtext NOT NULL,
    `Region` longtext NOT NULL,
    `PosX` float NOT NULL,
    `PosY` float NOT NULL,
    `PlaytimeSeconds` bigint NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);


CREATE TABLE `Avatars` (
    `PlayerAccountId` char(36) NOT NULL,
    `Content` longblob NOT NULL,
    `ContentType` varchar(40) NOT NULL,
    `Width` int NOT NULL,
    `Height` int NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`PlayerAccountId`),
    CONSTRAINT `FK_Avatars_Accounts_PlayerAccountId` FOREIGN KEY (`PlayerAccountId`) REFERENCES `Accounts` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `Sessions` (
    `Id` char(36) NOT NULL,
    `PlayerAccountId` char(36) NOT NULL,
    `TokenHash` varchar(64) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `RevokedAt` datetime(6) NULL,
    `LastSeenAt` datetime(6) NOT NULL,
    `UserAgent` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Sessions_Accounts_PlayerAccountId` FOREIGN KEY (`PlayerAccountId`) REFERENCES `Accounts` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveClaims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerSaveId` int NOT NULL,
    `RewardKey` varchar(64) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveClaims_Saves_PlayerSaveId` FOREIGN KEY (`PlayerSaveId`) REFERENCES `Saves` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveContainers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerSaveId` int NOT NULL,
    `ContainerKey` varchar(64) NOT NULL,
    `OpenedAt` datetime(6) NOT NULL,
    `Coin` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveContainers_Saves_PlayerSaveId` FOREIGN KEY (`PlayerSaveId`) REFERENCES `Saves` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveHeroes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerSaveId` int NOT NULL,
    `HeroKey` longtext NOT NULL,
    `Level` int NOT NULL,
    `Xp` int NOT NULL,
    `Recruited` tinyint(1) NOT NULL,
    `AttributePoints` int NOT NULL,
    `SkillPoints` int NOT NULL,
    `SpentStrength` int NOT NULL,
    `SpentDexterity` int NOT NULL,
    `SpentAgility` int NOT NULL,
    `SpentVitality` int NOT NULL,
    `SpentIntelligence` int NOT NULL,
    `SpentWisdom` int NOT NULL,
    `SpentDefense` int NOT NULL,
    `SpentLuck` int NOT NULL,
    `StatEnemiesDefeated` bigint NOT NULL,
    `StatBossesDefeated` bigint NOT NULL,
    `StatDeaths` bigint NOT NULL,
    `StatDamageDealt` bigint NOT NULL,
    `StatDamageTaken` bigint NOT NULL,
    `StatTreasuresOpened` bigint NOT NULL,
    `StatItemsCollected` bigint NOT NULL,
    `StatGoldEarned` bigint NOT NULL,
    `StatGoldSpent` bigint NOT NULL,
    `StatDistanceWalked` bigint NOT NULL,
    `StatPlaySeconds` bigint NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveHeroes_Saves_PlayerSaveId` FOREIGN KEY (`PlayerSaveId`) REFERENCES `Saves` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveTallies` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerSaveId` int NOT NULL,
    `Kind` varchar(16) NOT NULL,
    `TallyKey` varchar(40) NOT NULL,
    `Count` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveTallies_Saves_PlayerSaveId` FOREIGN KEY (`PlayerSaveId`) REFERENCES `Saves` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveContainerItems` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SaveContainerId` int NOT NULL,
    `Kind` varchar(16) NOT NULL,
    `TallyKey` varchar(40) NOT NULL,
    `Count` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveContainerItems_SaveContainers_SaveContainerId` FOREIGN KEY (`SaveContainerId`) REFERENCES `SaveContainers` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveEquipment` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SaveHeroId` int NOT NULL,
    `ItemId` varchar(40) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveEquipment_SaveHeroes_SaveHeroId` FOREIGN KEY (`SaveHeroId`) REFERENCES `SaveHeroes` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveHeroTallies` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SaveHeroId` int NOT NULL,
    `Kind` varchar(20) NOT NULL,
    `TallyKey` varchar(60) NOT NULL,
    `Count` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveHeroTallies_SaveHeroes_SaveHeroId` FOREIGN KEY (`SaveHeroId`) REFERENCES `SaveHeroes` (`Id`) ON DELETE CASCADE
);


CREATE TABLE `SaveSkills` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SaveHeroId` int NOT NULL,
    `SkillId` varchar(40) NOT NULL,
    `Rank` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SaveSkills_SaveHeroes_SaveHeroId` FOREIGN KEY (`SaveHeroId`) REFERENCES `SaveHeroes` (`Id`) ON DELETE CASCADE
);


INSERT INTO `Heroes` (`Key`, `BaseHp`, `Description`, `HeroClass`, `Name`, `Title`, `UnlockedByDefault`)
VALUES ('cleric', 140, 'A priest who traded his own descent into madness for a phial of panacea. Wades in with blessed plate and gilded mace, and can loose a nova of holy light.', 'Cleric', 'The Grieving Cleric', 'Whose Faith Fractured', FALSE);
SELECT ROW_COUNT();

INSERT INTO `Heroes` (`Key`, `BaseHp`, `Description`, `HeroClass`, `Name`, `Title`, `UnlockedByDefault`)
VALUES ('mage', 90, 'An exile of Kae Ychel with a greater demon bound inside his heart. Hurls discs of light — and, when pressed, unleashes the hellfire he can barely contain.', 'Mage', 'The Fallen Mage', 'Bearer of the Caged Fire', TRUE);
SELECT ROW_COUNT();

INSERT INTO `Heroes` (`Key`, `BaseHp`, `Description`, `HeroClass`, `Name`, `Title`, `UnlockedByDefault`)
VALUES ('thief', 100, 'A Seoshe gambler with a shard of the stolen crystal fused into his arm. Fires a crossbow from the dark and dashes through danger with reckless luck.', 'Thief', 'The Hollow Thief', 'Whose Crew the Crystal Took', TRUE);
SELECT ROW_COUNT();



CREATE UNIQUE INDEX `IX_Accounts_Email` ON `Accounts` (`Email`);


CREATE INDEX `IX_Accounts_ExternalProvider_ExternalSubjectId` ON `Accounts` (`ExternalProvider`, `ExternalSubjectId`);


CREATE INDEX `IX_SaveClaims_PlayerSaveId` ON `SaveClaims` (`PlayerSaveId`);


CREATE INDEX `IX_SaveContainerItems_SaveContainerId` ON `SaveContainerItems` (`SaveContainerId`);


CREATE INDEX `IX_SaveContainers_PlayerSaveId` ON `SaveContainers` (`PlayerSaveId`);


CREATE INDEX `IX_SaveEquipment_SaveHeroId` ON `SaveEquipment` (`SaveHeroId`);


CREATE INDEX `IX_SaveHeroes_PlayerSaveId` ON `SaveHeroes` (`PlayerSaveId`);


CREATE INDEX `IX_SaveHeroTallies_SaveHeroId` ON `SaveHeroTallies` (`SaveHeroId`);


CREATE INDEX `IX_SaveSkills_SaveHeroId` ON `SaveSkills` (`SaveHeroId`);


CREATE INDEX `IX_SaveTallies_PlayerSaveId` ON `SaveTallies` (`PlayerSaveId`);


CREATE INDEX `IX_Sessions_PlayerAccountId` ON `Sessions` (`PlayerAccountId`);


CREATE UNIQUE INDEX `IX_Sessions_TokenHash` ON `Sessions` (`TokenHash`);


