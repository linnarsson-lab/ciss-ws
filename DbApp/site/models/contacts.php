<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelContacts extends JModel {

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

  public function getContacts() {

    $db =& JFactory::getDBO();
    $query = ' SELECT c.id, contactperson, contactemail, contactphone, c.comment, c.user, c.time, 
                      COUNT(p.id) AS projectcount
               FROM #__aaacontact c
               LEFT JOIN #__aaaproject p ON p.#__aaacontactid = c.id
               GROUP BY c.id
     ';
    $db->setQuery($query);
//    $statement = $db->loadResult();
    $projects = $db->loadObjectList();
    return $projects;  

  }

}
