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
    $query = ' SELECT a.id, #__aaaprojectid AS projectid, p.plateid AS project, extraction_version, annotation_version,
               genome, transcript_db_version, transcript_variant, rpkm, a.comment, lanecount, resultspath, a.user,
               a.time, a.status AS status, p.title AS projecttitle, p.tissue, p.sampletype,
               CONVERT(CONCAT_WS("##", p.title, p.plateid, p.platereference, p.species, 
                 p.tissue, p.sampletype, p.collectionmethod, p.description, p.protocol, p.barcodeset, p.labbookpage,
                 p.layoutfile, p.comment, genome, transcript_db_version, transcript_variant, rpkm, a.comment,
                 resultspath, a.user, a.status,
                     
                      cli.principalinvestigator, cli.department, cli.address, cli.comment,
                      con.contactperson, con.contactemail, con.comment,
                      man.person, man.email, man.comment) USING utf8) COLLATE utf8_swedish_ci AS rowsearch
               FROM #__aaaanalysis a
               LEFT JOIN #__aaaproject p ON a.#__aaaprojectid = p.id
               LEFT JOIN #__aaaclient cli ON p.#__aaaclientid = cli.id
               LEFT JOIN #__aaacontact con ON p.#__aaacontactid = con.id
               LEFT JOIN #__aaamanager man ON p.#__aaamanagerid = man.id
               ORDER BY id DESC';
    $db->setQuery($query);
//    $statement = $db->loadResult();
    $item = $db->loadObjectList();
    return $item;  

  }

}
