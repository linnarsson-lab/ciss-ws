<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelClients extends JModel {

	protected $item;

	protected function populateState() {
		$app = JFactory::getApplication();
		// Get the message id
		$id = JRequest::getInt('id');
		$this->setState('message.id', $id);

		// Load the parameters.
		$params = $app->getParams();
		$this->setState('params', $params);
		parent::populateState();
	}

//  require_once JPATH_COMPONENT_ADMINISTRATOR.DS.'tables'.DS.'clients.php';

//	public function getTable($type = 'Clients', $prefix = 'DbAppTable', $config = array()) {
//     JError::raiseWarning(500, JText::_(' Clients models l.25 '));

//		return JTable::getInstance($type, $prefix, $config);
//	}

  public function getClients() {

    $db =& JFactory::getDBO();
    $query = ' SELECT #__aaaclient.id AS id, principalinvestigator, department, address, vatno, #__aaaclient.comment AS comment, #__aaaclient.user AS user, #__aaaclient.time AS time, #__categories.title as category, COUNT(#__aaaproject.id) AS projectcount
     FROM #__aaaclient
     LEFT JOIN #__categories ON #__aaaclient.catid = #__categories.id
     LEFT JOIN #__aaaproject ON #__aaaproject.#__aaaclientid = #__aaaclient.id
        GROUP BY #__aaaclient.id
        ORDER BY #__aaaclient.id DESC  ';
    $db->setQuery($query);
//    $statement = $db->loadResult();
    $clients = $db->loadObjectList();
    return $clients;  

  }

	public function getItems() {

//      JError::raiseWarning(500, JText::_('DbAppModelClients   <wrong>:  ' . get_class($this)));

//     $db = JFactory::getDBO();
//     $query = $db->getQuery(true);
//     $query->select('*');
//     $query->from('#__aaaclient');
//     $db->setQuery((string)$query);
//     $this->items = $db->loadAssocList();

		if (!isset($this->items)) {
			$id = $this->getState('message.id');
			$this->_db->setQuery($this->_db->getQuery(true)
				->from('#__aaaclient')
				->leftJoin('#__categories ON #__aaaclient.catid=#__categories.id')
				->select('#__aaaclient.id AS id, principalinvestigator, department, address, vatno, comment, #__categories.title as category'));
//				->where('cl.id=' . (int)$id)
			if (!$this->items = $this->_db->loadObjectList()) {
				$this->setError($this->_db->getError());
			}	else {

				// Load the JSON string
//				$params = new JRegistry;
//				$params->loadJSON($this->item->params);
//				$this->item->params = $params;

				// Merge global params with item params
//				$params = clone $this->getState('params');
//				$params->merge($this->item->params);
//				$this->item->params = $params;
			}
		}
		return $this->items;
	}
}
