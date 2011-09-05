<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelSequencingPrimer extends JModel {

  protected $item;

  public function getItem() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
//    echo $searchid;
    $query = "  SELECT id, primername, sequence, comment, user, time 
               FROM #__aaasequencingprimer
               WHERE id = '" . $searchid . "' ";
    $db->setQuery($query);
//    echo $query;
    $item = $db->loadObject();
    return $item;  
  }

//	protected function populateState() {
//		$app = JFactory::getApplication();
//		// Get the message id
//		$id = JRequest::getInt('id');
//		$this->setState('message.id', $id);

		// Load the parameters.
//		$params = $app->getParams();
//		$this->setState('params', $params);
//		parent::populateState();
//	}

//	public function getTable($type = 'Manager', $prefix = 'DbAppTable', $config = array()) {
//		return JTable::getInstance($type, $prefix, $config);
//	}

//  public function getForm($data = array(), $loadData = true) {
    // Get the form.
//    $form = $this->loadForm('com_dbapp.manager', 'manager', array('control' => 'jform', 'load_data' => $loadData));
//    if (empty($form)) {
//      return false;
//    }
//    return $form;
//  }

//  public function save($data = array()) {
//    $db =& JFactory::getDBO();
//JError::raiseWarning('500', JText::_('[from site/models/project.php]C ' . JRequest::getVar('c')));

//    $db->updateObject('#__aaamanager', &$data, 'id');
//    if ($this->setError($db->getErrorMsg())) {
//      return false;
//    } else {
//      return true;
//    }
//  }

//  protected function loadFormData() {
    // Check the session for previously entered form data.
//    $data = JFactory::getApplication()->getUserState('com_dbapp.edit.manager.data', array());
//    if (empty($data)) {
//      $data = $this->getItem();
//    }
//    return $data;
//  }



}
