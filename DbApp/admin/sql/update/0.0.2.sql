-- 2011-05-04 Rikard Erlandsson
ALTER TABLE `joomla`.`#__aaaproject` ADD COLUMN `status` ENUM('inqueue', 'processing', 'done', 'failed') DEFAULT NULL AFTER `layoutfile` ;


