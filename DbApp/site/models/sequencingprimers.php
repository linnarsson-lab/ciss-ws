<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelSequencingPrimers extends JModel {

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
    $query = ' SELECT id, primername, sequence, comment, user, time 
               FROM #__aaasequencingprimer     ';
    $db->setQuery($query);
//    $statement = $db->loadResult();
    $item = $db->loadObjectList();
    return $item;  

  }

}
