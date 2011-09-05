<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.database.table');

class DbAppTableLane extends JTable {

	function __construct(&$db) {
		parent::__construct('#__aaalane', 'id', $db);
	}

	protected function _getAssetName() {
		$k = $this->_tbl_key;
		return 'com_dbapp.message.'.(int) $this->$k;
	}

	protected function _getAssetTitle()	{
		return $this->lane;
	}

	protected function _getAssetParentId() {
		$asset = JTable::getInstance('Asset');
		$asset->loadByName('com_dbapp');
		return $asset->id;
	}

}
