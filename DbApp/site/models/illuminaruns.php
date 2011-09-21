<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelIlluminaRuns extends JModel {

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
    $query = ' SELECT r.id AS id, illuminarunid, r.comment, rundate, 
           r.user AS user, r.time AS time, r.title AS title, rundocument, r.status AS status, runno,
           GROUP_CONCAT(DISTINCT(plateid) ORDER BY plateid ASC) AS plateids, 
           GROUP_CONCAT(DISTINCT(p.id) ORDER BY plateid ASC) AS platedbids
               FROM #__aaailluminarun r 
               LEFT JOIN #__aaalane ON #__aaalane.#__aaailluminarunid = r.id 
               LEFT JOIN #__aaasequencingbatch ON #__aaalane.#__aaasequencingbatchid = #__aaasequencingbatch.id 
               LEFT JOIN #__aaaproject p ON p.id = #__aaasequencingbatch.#__aaaprojectid 
               GROUP BY r.id 
               ORDER BY illuminarunid DESC ';
    $db->setQuery($query);
//    $statement = $db->loadResult();
    $item = $db->loadObjectList();
    return $item;  

  }

}
