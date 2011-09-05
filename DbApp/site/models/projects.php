<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelProjects extends JModel {

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


  public function getProjects() {

    $db =& JFactory::getDBO();
    $query = ' SELECT p.id as id, principalinvestigator, person, contactperson, plateid, p.status, platereference,
                 barcodeset, species, tissue, sampletype, collectionmethod, weightconcentration, fragmentlength,
                 p.molarconcentration, p.user as user, p.time as time, layoutfile, 
                 COUNT(DISTINCT(a.id)) AS analysiscount,
                 SUBSTRING_INDEX(GROUP_CONCAT(CAST(a.status AS CHAR) ORDER BY a.id DESC), ",", 1) AS astatus,
                 COUNT(DISTINCT(l.id)) AS assignedlanes, IFNULL(nplannedlanes, 0) AS plannedlanes, 
                 IFNULL(nbatches, 0) AS batches
               FROM #__aaaproject p
               LEFT JOIN #__aaaclient ON p.#__aaaclientid = #__aaaclient.id
               LEFT JOIN #__aaacontact ON p.#__aaacontactid = #__aaacontact.id
               LEFT JOIN #__aaamanager ON p.#__aaamanagerid = #__aaamanager.id
               LEFT JOIN #__aaasequencingbatch b ON p.id = b.#__aaaprojectid
               LEFT JOIN #__aaalane l ON b.id = l.#__aaasequencingbatchid
 LEFT JOIN (SELECT b.#__aaaprojectid AS bpid, SUM(b.plannednumberoflanes) AS nplannedlanes, COUNT(b.id) AS nbatches
                         FROM #__aaasequencingbatch b GROUP BY b.#__aaaprojectid) AS bsums
       ON bpid = p.id
 LEFT JOIN #__aaaanalysis a ON a.#__aaaprojectid = p.id
 WHERE p.id > 0 GROUP BY p.id ORDER BY p.id DESC   ';

    $db->setQuery($query);
//    $statement = $db->loadResult();
    $projects = $db->loadObjectList();
    return $projects;  

  }

}
