<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelIlluminaRun extends JModel {

  protected $item;

  public function getItems() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid');
    $query = " SELECT r.id AS id, illuminarunid, r.comment, rundate, r.user AS user, r.time AS time, r.status AS status,
                r.title AS title, r.labbookpage AS labbookpage, r.cycles, r.indexcycles, r.pairedcycles, plateid, 
                rundocument, runno, p.id AS projectid, c.principalinvestigator AS pi, contactperson, contactemail, 
                l.id AS Lid, laneno, l.molarconcentration AS molarconcentration, b.title AS batchtitle, b.id AS batchid,
                b.plannednumberofcycles AS plannedcycles, b.plannedindexcycles AS plannedindexcycles, yield, 
                pfyield, l.status AS Lstatus, l.comment AS Lcomment, l.user AS Luser, l.time AS Ltime, b.id AS Sid
               FROM #__aaailluminarun r
               LEFT JOIN #__aaalane l ON l.#__aaailluminarunid = r.id 
               LEFT JOIN #__aaasequencingbatch b ON l.#__aaasequencingbatchid = b.id 
               LEFT JOIN #__aaaproject p ON b.#__aaaprojectid = p.id
               LEFT JOIN #__aaaclient c ON p.#__aaaclientid = c.id
               LEFT JOIN #__aaacontact ctc ON p.#__aaacontactid = ctc.id
               WHERE r.id = '" . $searchid . "' 
               ORDER BY laneno ASC ";
    $db->setQuery($query);
    $item = $db->loadObjectList();
    return $item;
  }

  public function getItem() {
#######       READ in directory info from config file    ######################################
    $xmlfile = JPATH_COMPONENT_ADMINISTRATOR.DS.'config.txt';
    $string = rtrim(file_get_contents($xmlfile));
    $words = preg_split("/\s/", $string);
    $xmlstring = file_get_contents($words[1]);
    preg_match("/<RunsFolder\>(.*)<\/RunsFolder>/", $xmlstring, $matches);
    $runsfolder = $matches[1];
    preg_match("/<ProjectsFolder\>(.*)<\/ProjectsFolder>/", $xmlstring, $matches);
    $projectsfolder = $matches[1];
    preg_match("/<UploadsFolder>(.*)<\/UploadsFolder>/", $xmlstring, $matches);
    $uploadsfolder = $matches[1];
##############################################################################################
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
    // echo $searchid;
    $query = " SELECT id, illuminarunid, runno, status, title, labbookpage, rundocument, comment, rundate, 
                      user, time, cycles, indexcycles, pairedcycles
               FROM #__aaailluminarun     
               WHERE id = '" . $searchid . "'    ";

    $db->setQuery($query);
    $item = $db->loadObject();
    if (!empty($item->rundocument)) {
      $target_path = $uploadsfolder . "/" . $item->rundocument; 
    } else {
      $target_path = $uploadsfolder . "ingenfil"; 
    }
    if (file_exists($target_path)) {
      $item->fileupload = 1;
    } else {
      $item->fileupload = -1;
    }
    return $item;  
  }

	protected function populateState() {
		$app = JFactory::getApplication();
		// Get the message id
		$id = JRequest::getInt('id');
		$this->setState('message.id', $id);

		parent::populateState();
	}



}
