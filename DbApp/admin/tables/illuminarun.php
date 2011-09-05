<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.database.table');

class DbAppTableIlluminaRun extends JTable {

	function __construct(&$db) {
		parent::__construct('#__aaailluminarun', 'id', $db);
	}

	protected function _getAssetName() {
		$k = $this->_tbl_key;
		return 'com_dbapp.message.'.(int) $this->$k;
	}

	protected function _getAssetTitle()	{
		return $this->illuminarun;
	}

	protected function _getAssetParentId() {
		$asset = JTable::getInstance('Asset');
		$asset->loadByName('com_dbapp');
		return $asset->id;
	}

}
