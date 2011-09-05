<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelManagers extends JModel {

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

  public function getItems() {

    $db =& JFactory::getDBO();
    $query = ' SELECT m.id, m.person, m.email, m.phone, m.comment, m.user, m.time, COUNT(p.id) AS projectcount
               FROM #__aaamanager m
               LEFT JOIN #__aaaproject p ON p.#__aaamanagerid = m.id
               GROUP BY m.id';
    $db->setQuery($query);
//    $statement = $db->loadResult();
    $item = $db->loadObjectList();
    return $item;  

  }

}
