<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.database.table');

class DbAppTableClient extends JTable {

	function __construct(&$db) {
		parent::__construct('#__aaaclient', 'id', $db);
	}

//	public function bind($array, $ignore = '') {
//		if (isset($array['params']) && is_array($array['params'])) {
			// Convert the params field to a string.
//			$parameter = new JRegistry;
//			$parameter->loadArray($array['params']);
//			$array['params'] = (string)$parameter;
//		}
//		return parent::bind($array, $ignore);
//	}

//	public function load($pk = null, $reset = true) {
//		if (parent::load($pk, $reset)) {
			// Convert the params field to a registry.
//			$params = new JRegistry;
//			$params->loadJSON($this->params);
//			$this->params = $params;
//			return true;
//		}	else {
//			return false;
//		}
//	}

	protected function _getAssetName() {
		$k = $this->_tbl_key;
		return 'com_dbapp.message.'.(int) $this->$k;
	}

	protected function _getAssetTitle()	{
		return $this->project;
	}

	protected function _getAssetParentId() {
		$asset = JTable::getInstance('Asset');
		$asset->loadByName('com_dbapp');
		return $asset->id;
	}

}
