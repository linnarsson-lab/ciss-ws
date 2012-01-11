<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelLane extends JModel {

	protected $item;

  public function getItem() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
//    echo $searchid;                  aaasequencingbatchid
    $query = " SELECT l.id as id, r.id as aaailluminarunid, illuminarunid, rundate, 
                b.id as aaasequencingbatchid, laneno, cycles, molarconcentration, 
                yield, l.status AS Lstatus, l.comment as comment, l.user as user, l.time as time
               FROM #__aaalane l 
               LEFT JOIN #__aaailluminarun r ON l.#__aaailluminarunid = r.id
               LEFT JOIN #__aaasequencingbatch b ON l.#__aaasequencingbatchid = b.id
               WHERE l.id = '" . $searchid . "' ";

    $db->setQuery($query);
//    echo $query;
    $item = $db->loadObject();
    return $item;  
  }

	protected function populateState() {
		$app = JFactory::getApplication();
		// Get the message id
		$id = JRequest::getInt('id');
		$this->setState('message.id', $id);

		parent::populateState();
	}

	public function getTable($type = 'Lane', $prefix = 'DbAppTable', $config = array()) {
		return JTable::getInstance($type, $prefix, $config);
	}

  public function getForm($data = array(), $loadData = true) {
    // Get the form.
    $form = $this->loadForm('com_dbapp.lane', 'lane', array('control' => 'jform', 'load_data' => $loadData));
    if (empty($form)) {
      return false;
    }
    return $form;
  }

  public function save($data = array()) {
    $db =& JFactory::getDBO();
JError::raiseWarning('500', JText::_('[from site/models/project.php]C ' . JRequest::getVar('c')));

    $db->updateObject('#__aaalane', &$data, 'id');
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
    $data = JFactory::getApplication()->getUserState('com_dbapp.edit.lane.data', array());
    if (empty($data)) {
      $data = $this->getItem();
    }
    return $data;
  }

}
