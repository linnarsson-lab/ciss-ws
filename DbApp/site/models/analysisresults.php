<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelAnalysisresults extends JModel {

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
    $query = ' SELECT a.id, #__aaaprojectid AS projectid, p.title AS project, extraction_version, annotation_version,
               genome, transcript_db_version, transcript_variant, a.comment, lanecount, resultspath, a.user, a.time,
               a.status AS status
               FROM #__aaaanalysis a
               LEFT JOIN #__aaaproject p ON a.#__aaaprojectid = p.id
               ORDER BY projectid DESC';
    $db->setQuery($query);
//    $statement = $db->loadResult();
    $item = $db->loadObjectList();
    return $item;  

  }

}
