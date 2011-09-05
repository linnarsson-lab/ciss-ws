<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelClient extends JModel {

	protected $item;

  public function getClient() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
//    echo $searchid;
    $query = "  SELECT #__aaaclient.id AS id, principalinvestigator, department, address, catid,
             vatno, comment, user, time, #__categories.title as category 
               FROM #__aaaclient
               LEFT JOIN #__categories ON #__aaaclient.catid=#__categories.id WHERE #__aaaclient.id = '" . $searchid . "' ";
    $db->setQuery($query);
//    echo $query;
    $client = $db->loadObject();
    return $client;  
  }

	protected function populateState() {
		$app = JFactory::getApplication();
		// Get the message id
		$id = JRequest::getInt('id');
		$this->setState('message.id', $id);

		// Load the parameters.
//		$params = $app->getParams();
//		$this->setState('params', $params);
		parent::populateState();
	}

	public function getTable($type = 'Client', $prefix = 'DbAppTable', $config = array()) {
		return JTable::getInstance($type, $prefix, $config);
	}

  public function getForm($data = array(), $loadData = true) {
    // Get the form.
    $form = $this->loadForm('com_dbapp.client', 'client', array('control' => 'jform', 'load_data' => $loadData));
    if (empty($form)) {
      return false;
    }
    return $form;
  }

  public function save($data = array()) {
    $db =& JFactory::getDBO();
JError::raiseWarning('500', JText::_('[from site/models/client.php]C ' . JRequest::getVar('c')));

//    $query = " UPDATE #__aaaclient SET ";
//    $query .= " principalinvestigator = " . $db->Quote($data->principalinvestigator) ;
//    $query .= ", department = " . $db->Quote($data->department) ;
//    $query .= ", address = " . $db->Quote($data->address) ;
//    $query .= ", vatno = " . $db->Quote($data->vatno) ;
//    $query .= ", contactperson = " . $db->Quote($data->contactperson) ;
//    $query .= ", contactemail = " . $db->Quote($data->contactemail) ;
//    $query .= ", contactphone = " . $db->Quote($data->contactphone) ;
//    $query .= " WHERE id = " . $db->Quote($data->id) ;
//    $db->setQuery($query);
    $db->updateObject('#__aaaclient', &$data, 'id');
    if ($this->setError($db->getErrorMsg())) {
      return false;
    } else {
      return true;
    }
  }
//  public function getScript() {
//    return 'administrator/components/com_dbapp/models/forms/tabone.js';
//  }

  protected function loadFormData() {
    // Check the session for previously entered form data.
    $data = JFactory::getApplication()->getUserState('com_dbapp.edit.client.data', array());
    if (empty($data)) {
      $data = $this->getItem();
    }
    return $data;
  }



}
