<?php
defined('_JEXEC') or die('Restricted access');

/** * Script file of DbApp component */
class com_dbappInstallerScript {

	function install($parent) {
		// $parent is the class calling this method
		$parent->getParent()->setRedirectURL('index.php?option=com_dbapp');
	}

	function uninstall($parent) {
		// $parent is the class calling this method
		echo '<p>' . JText::_('Uninstall of DbApp') . '</p>';
	}

	function update($parent) {
		// $parent is the class calling this method
		echo '<p>' . JText::_('Update of DbApp') . '</p>';
	}

	function preflight($type, $parent) {
		// $parent is the class calling this method
		// $type is the type of change (install, update or discover_install)
		echo '<p>' . JText::_('Starting to update ' . $type . ' DbApp') . '</p>';
	}

	function postflight($type, $parent) {
		// $parent is the class calling this method
		// $type is the type of change (install, update or discover_install)
		echo '<p>' . JText::_('Done updating ' . $type . ' DbApp') . '</p>';
	}
}
