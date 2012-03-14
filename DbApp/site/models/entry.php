<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelEntry extends JModel {

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

  public function getBuptasks() {
    $db =& JFactory::getDBO();
    $query = ' SELECT id, path, status, priority, time FROM #__aaabackupqueue ORDER BY status DESC, id DESC ';
    $db->setQuery($query);
    $buptasks = $db->loadObjectList();
    return $buptasks;  
  }
  public function getMailtasks() {
    $db =& JFactory::getDBO();
    $query = ' SELECT id, runno, laneno, email, status, time FROM #__aaafqmailqueue ORDER BY status, runno DESC, laneno ';
    $db->setQuery($query);
    $mailtasks = $db->loadObjectList();
    return $mailtasks;  
  }

  public function getProjects() {
    $db =& JFactory::getDBO();
    $query = " SELECT #__aaaproject.id as id, principalinvestigator, #__aaaclient.id as aaaclientid, person, #__aaamanager.id as aaamanagerid, contactperson, #__aaacontact.id as aaacontactid, title, plateid, barcodeset, species, tissue, sampletype, collectionmethod, weightconcentration, fragmentlength, molarconcentration, labbookpage, protocol, #__aaaproject.comment as comment, #__aaaproject.user as user, #__aaaproject.time as time
               FROM #__aaaproject 
               LEFT JOIN #__aaaclient ON #__aaaproject.#__aaaclientid = #__aaaclient.id
               LEFT JOIN #__aaacontact ON #__aaaproject.#__aaacontactid = #__aaacontact.id
               LEFT JOIN #__aaamanager ON #__aaaproject.#__aaamanagerid = #__aaamanager.id "   ;
    $db->setQuery($query);
    $clients = $db->loadObjectList();
    return $clients;  
  }

  public function getClients() {
    $db =& JFactory::getDBO();
    $query = ' SELECT #__aaaclient.id AS id, principalinvestigator, department, address, vatno, comment, user, time, #__categories.title as category 
               FROM #__aaaclient
               LEFT JOIN #__categories ON #__aaaclient.catid=#__categories.id  ';
    $db->setQuery($query);
    $clients = $db->loadObjectList();
    return $clients;  
  }

}
?>
