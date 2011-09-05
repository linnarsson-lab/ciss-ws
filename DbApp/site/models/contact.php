<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelContact extends JModel {

  public function getContact() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
//    echo $searchid;
    $query = "  SELECT id, contactperson, contactemail, contactphone, comment, user, time 
               FROM #__aaacontact
               WHERE id = '" . $searchid . "' ";
    $db->setQuery($query);
//    echo $query;
    $contact = $db->loadObject();
    return $contact;  
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

	public function getTable($type = 'Contact', $prefix = 'DbAppTable', $config = array()) {
		return JTable::getInstance($type, $prefix, $config);
	}

  public function getForm($data = array(), $loadData = true) {
    // Get the form.
    $form = $this->loadForm('com_dbapp.contact', 'contact', array('control' => 'jform', 'load_data' => $loadData));
    if (empty($form)) {
      return false;
    }
    return $form;
  }

  public function save($data = array()) {
    $db =& JFactory::getDBO();
JError::raiseWarning('500', JText::_('[from site/models/project.php]C ' . JRequest::getVar('c')));

    $db->updateObject('#__aaacontact', &$data, 'id');
    if ($this->setError($db->getErrorMsg())) {
      return false;
    } else {
      return true;
    }
  }

  protected function loadFormData() {
    // Check the session for previously entered form data.
    $data = JFactory::getApplication()->getUserState('com_dbapp.edit.contact.data', array());
    if (empty($data)) {
      $data = $this->getItem();
    }
    return $data;
  }



}
