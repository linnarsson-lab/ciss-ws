SET foreign_key_checks = 0;

DROP TABLE IF EXISTS `#__aaaclient`;
CREATE TABLE `#__aaaclient` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `catid` int(11) NOT NULL DEFAULT 0,
  `principalinvestigator` varchar(255) DEFAULT '',
  `department` varchar(255) DEFAULT '',
  `address` varchar(255) DEFAULT '',
  `vatno` varchar(255) DEFAULT '',
  `comment` text,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` varchar(255) DEFAULT '',
  `time` datetime DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaacontact`;
CREATE TABLE `#__aaacontact` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `contactperson` varchar(255) DEFAULT '',
  `contactemail` varchar(255) DEFAULT '',
  `contactphone` varchar(255) DEFAULT '',
  `comment` text,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` varchar(255) DEFAULT '',
  `time` datetime DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaamanager`;
CREATE TABLE `#__aaamanager` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `person` varchar(255) DEFAULT '',
  `email` varchar(255) DEFAULT '',
  `phone` varchar(255) DEFAULT '',
  `comment` text,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` varchar(255) DEFAULT '',
  `time` datetime DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaailluminarun`;
CREATE TABLE `#__aaailluminarun` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `illuminarunid` VARCHAR(255) DEFAULT NULL,
  `title` VARCHAR(255) DEFAULT NULL,
  `labbookpage` VARCHAR(255) DEFAULT '',
  `rundocument` VARCHAR(255) DEFAULT NULL,
  `comment` text,
  `rundate` date DEFAULT '0000-00-00 00:00:00',
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME,
  `ordering` INT(11),
  `param` TEXT,
  `user` varchar(255) DEFAULT NULL,
  `time` datetime DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaaproject`;
CREATE TABLE `#__aaaproject` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `#__aaacontactid` int(11) DEFAULT NULL,
  `#__aaamanagerid` int(11) DEFAULT NULL,
  `#__aaaclientid` int(11) DEFAULT NULL,
  `title` varchar(255) DEFAULT NULL,
  `productiondate` varchar(255) DEFAULT '0000-00-00 00:00:00',
  `plateid` varchar(255) DEFAULT NULL,
  `platereference` varchar(255) DEFAULT NULL,
  `species` varchar(255) DEFAULT '',
  `tissue` varchar(255) DEFAULT '',
  `sampletype` varchar(255) DEFAULT '',
  `collectionmethod` varchar(255) DEFAULT '',
  `weightconcentration` DECIMAL(6,3) DEFAULT NULL,
  `fragmentlength` INT(11) DEFAULT NULL,
  `molarconcentration` DECIMAL(6,3) DEFAULT NULL,
  `description` blob COMMENT 'excel-sheet',
  `protocol` varchar(255) DEFAULT '',
  `barcodeset` varchar(255) DEFAULT '',
  `labbookpage` varchar(255) DEFAULT '',
  `layoutfile` VARCHAR(255) DEFAULT '',
  `comment` text,
  `status` ENUM('inqueue','processing','ready','failed') DEFAULT 'ready',
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` varchar(255) DEFAULT NULL,
  `time` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  FOREIGN KEY `fk_aaa_project_1` (`#__aaacontactid`) REFERENCES `#__aaacontact` (`id`) ON DELETE NO ACTION ON UPDATE CASCADE,
  FOREIGN KEY `fk_aaa_project_2` (`#__aaamanagerid`) REFERENCES `#__aaamanager` (`id`) ON DELETE NO ACTION ON UPDATE CASCADE,
  FOREIGN KEY `fk_aaa_project_3` (`#__aaaclientid`) REFERENCES `#__aaaclient` (`id`) ON DELETE NO ACTION ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaasequencingbatch`;
CREATE TABLE `#__aaasequencingbatch` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `#__aaaprojectid` int(11) DEFAULT NULL,
  `#__aaasequencingprimerid` INT(11) DEFAULT NULL,
  `indexprimerid` INT(11) DEFAULT NULL,
  `title` varchar(255) DEFAULT '',
  `plannednumberoflanes` VARCHAR(255) DEFAULT NULL,
  `plannednumberofcycles` VARCHAR(255) DEFAULT NULL,
  `plannedindexcycles` varchar(255) DEFAULT NULL,
  `cost` varchar(255) DEFAULT NULL,
  `invoice` enum('sent','not sent') DEFAULT 'not sent',
  `signed` enum('yes','no') DEFAULT 'no',
  `labbookpage` varchar(255) DEFAULT '',
  `comment` text,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` varchar(255) DEFAULT NULL,
  `time` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  FOREIGN KEY `fk_aaa_sequencingbatch_1` (`#__aaaprojectid`) REFERENCES `#__aaaproject` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  FOREIGN KEY `fk_aaa_sequencingbatch_2` (`#__aaasequencingprimerid`) REFERENCES `#__aaasequencingprimer` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  FOREIGN KEY `fk_aaa_sequencingbatch_3` (`indexprimerid`) REFERENCES `#__aaasequencingprimer` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaalane`;
CREATE TABLE `#__aaalane` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `#__aaailluminarunid` int(11) DEFAULT NULL,
  `#__aaasequencingbatchid` int(11) DEFAULT NULL,
  `laneno` int(11) DEFAULT NULL,
  `cycles` int(11) DEFAULT NULL,
  `molarconcentration` varchar(255) DEFAULT NULL,
  `yield` varchar(255) DEFAULT NULL,
  `comment` text,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` varchar(255) DEFAULT NULL,
  `time` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  FOREIGN KEY `fk_aaa_lane_2` (`#__aaailluminarunid`) REFERENCES `#__aaailluminarun` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  FOREIGN KEY `fk_aaa_lane_1` (`#__aaasequencingbatchid`) REFERENCES `#__aaasequencingbatch` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaasequencingprimer`;
CREATE TABLE `#__aaasequencingprimer` (
  `id` INT(11) NOT NULL AUTO_INCREMENT,
  `primername` VARCHAR(255) DEFAULT NULL,
  `sequence` VARCHAR(255) DEFAULT NULL,
  `comment` TEXT,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` VARCHAR(255) DEFAULT NULL,
  `time` DATETIME DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaaanalysis`;
CREATE TABLE `#__aaaanalysis` (
  `id` INT(11) NOT NULL AUTO_INCREMENT,
  `#__aaaprojectid` int(11) NOT NULL,
  `extraction_version` VARCHAR(255) DEFAULT NULL,
  `annotation_version` VARCHAR(255) DEFAULT NULL,
  `genome` VARCHAR(255) DEFAULT NULL,
  `transcript_db_version` VARCHAR(255) DEFAULT NULL,
  `transcript_variant` ENUM('all', 'single') DEFAULT NULL,
  `comment` TEXT,
  `lanecount` INT(11),
  `resultspath` VARCHAR(255) DEFAULT NULL,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` VARCHAR(255) DEFAULT NULL,
  `time` DATETIME DEFAULT NULL,
  PRIMARY KEY (`id`),
  FOREIGN KEY `fk_aaa_analysis_1` (`#__aaaprojectid`) REFERENCES `#__aaaproject` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `#__aaafqmailqueue`;
CREATE TABLE `#__aaafqmailqueue` (
  `id` INT(11) NOT NULL AUTO_INCREMENT,
  `runno` int(11) NOT NULL,
  `laneno` VARCHAR(255) DEFAULT NULL,
  `email` VARCHAR(255) DEFAULT NULL,
  `status` VARCHAR(255) DEFAULT NULL,
  `published` TINYINT DEFAULT 1,
  `hits` INT(11),
  `checked_out` INT(11) DEFAULT 0,
  `checked_out_time` DATETIME DEFAULT '0000-00-00 00:00:00',
  `ordering` INT(11),
  `param` TEXT,
  `user` VARCHAR(255) DEFAULT NULL,
  `time` DATETIME DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;


SET foreign_key_checks = 1;

INSERT INTO `joomla`.`#__aaaclient` (`id`, `principalinvestigator`, `comment`, `user`, `time`)
VALUES ('1', 'Not known', 'N.B. do not edit this record', 'install', NOW());

INSERT INTO `joomla`.`#__aaacontact` (`id`, `contactperson`, `comment`, `user`, `time`)
VALUES ('1', 'Not known', 'N.B. do not edit this record', 'install', NOW());

INSERT INTO `joomla`.`#__aaamanager` (`id`, `person`, `comment`, `user`, `time`)
VALUES ('1', 'Not known', 'N.B. do not edit this record', 'install', NOW());

INSERT INTO `joomla`.`#__aaaproject` (`id`, `#__aaacontactid`, `#__aaamanagerid`, `#__aaaclientid`, `title`, `comment`, `user`, `time`)
VALUES ('1', '1', '1', '1', 'Not known', 'N.B. do not edit this record', 'install', NOW());

INSERT INTO `joomla`.`#__aaasequencingbatch` (`id`, `#__aaaprojectid`, `title`, `comment`, `user`, `time`)
VALUES ('1', '1', 'Not known', 'N.B. do not edit this record', 'install', NOW());

INSERT INTO `joomla`.`#__aaasequencingprimer` (`id`, `primername`, `comment`, `user`, `time`)
VALUES ('1', 'Not used', 'N.B. do not edit this record', 'install', NOW());

