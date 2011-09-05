<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelSequencingBatch extends JModel {

//  protected $item;

  public function getItem() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
    $query = " SELECT b.id AS id, #__aaaprojectid AS aaaprojectid, principalinvestigator, p.title AS title,
                      p.plateid AS plateid, p.status AS platestatus, #__aaasequencingprimerid AS primerid,
                      indexprimerid,
                      plannednumberoflanes, plannednumberofcycles, cost, invoice, signed, b.labbookpage AS labbookpage,
                      b.title AS sr_title, plannedindexcycles, b.comment AS comment, b.user AS user, b.time AS time,
                      seqprim.primername AS primer, indprim.primername AS indexprimer
               FROM #__aaasequencingbatch b
               LEFT JOIN #__aaaproject p ON b.#__aaaprojectid = p.id 
               LEFT JOIN #__aaaclient ON p.#__aaaclientid = #__aaaclient.id
               LEFT JOIN #__aaasequencingprimer seqprim ON b.#__aaasequencingprimerid = seqprim.id
               LEFT JOIN #__aaasequencingprimer indprim ON b.#__aaasequencingprimerid = indprim.id
               WHERE b.id = '" . $searchid . "' ";

    $db->setQuery($query);
    $item = $db->loadObject();
    return $item;  
  }

  public function getIlluminaRuns() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
    $query = " SELECT #__aaailluminarun.id AS id, #__aaailluminarun.illuminarunid AS RunNo,
                      GROUP_CONCAT(laneno) AS lanes
               FROM #__aaasequencingbatch     
            LEFT JOIN #__aaaproject ON #__aaasequencingbatch.#__aaaprojectid = #__aaaproject.id
            LEFT JOIN #__aaalane ON #__aaalane.#__aaasequencingbatchid = #__aaasequencingbatch.id
            INNER JOIN #__aaailluminarun ON #__aaalane.#__aaailluminarunid = #__aaailluminarun.id
               WHERE #__aaasequencingbatch.id = '" . $searchid . "' 
            GROUP BY RunNo ORDER BY RunNo, laneno ";

    $db->setQuery($query);
    $item = $db->loadObjectList();
    return $item;  
  }

  protected function populateState() {
    $app = JFactory::getApplication();
    // Get the message id
    $id = JRequest::getInt('id');
    $this->setState('message.id', $id);
    parent::populateState();
  }

  public function getTable($type = 'SequencingBatch', $prefix = 'DbAppTable', $config = array()) {
    return JTable::getInstance($type, $prefix, $config);
  }

  public function getForm($data = array(), $loadData = true) {
    $form = $this->loadForm('com_dbapp.sequencingbatch', 'sequencingbatch',
                            array('control' => 'jform', 'load_data' => $loadData));
    if (empty($form)) {
      return false;
    }
    return $form;
  }

  public function save($data = array()) {
    $db =& JFactory::getDBO();
    JError::raiseWarning('500', JText::_('[from site/models/project.php]C ' . JRequest::getVar('c')));
    $db->updateObject('#__aaasequencingbatch', $data, 'id');
    if ($this->setError($db->getErrorMsg())) {
      return false;
    } else {
      return true;
    }
  }

  protected function loadFormData() {
    // Check the session for previously entered form data.
    $data = JFactory::getApplication()->getUserState('com_dbapp.edit.sequencingbatch.data', array());
    if (empty($data)) {
      $data = $this->getItem();
    }
    return $data;
  }

}
