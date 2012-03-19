<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelProject extends JModel {

	protected $item;

  public function getProject() {

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
    $searchid = JRequest::getVar('searchid');
    $inqueue = JRequest::getVar('inqueue');
    $cancel = JRequest::getVar('cancel', "");
    $rescancel = JRequest::getVar('rescancel', "0");
    if ($rescancel > 0) {
      $query = " UPDATE #__aaaanalysis SET status='cancelled' WHERE id='$rescancel' ";
      $db->setQuery($query);
      if ($db->query()) {
        JError::raiseNotice('Message', JText::_("Analysis Id $rescancel Status-> 'cancelled'"));
        $item->status = "cancelled";
      } else {
        JError::raiseWarning('Message', JText::_( "[ " . $db->getErrorMsg() . " ]"));
      }
    }
    $resretry = JRequest::getVar('resretry', "0");
    if ($resretry > 0) {
      $query = " UPDATE #__aaaanalysis SET status='inqueue' WHERE id='$resretry' ";
      $db->setQuery($query);
      if ($db->query()) {
        JError::raiseNotice('Message', JText::_("Analysis Id $resretry Status-> 'inqueue'"));
        $item->status = "inqueue";
      } else {
        JError::raiseWarning('Message', JText::_( "[ " . $db->getErrorMsg() . " ]"));
      }
    }

    $query = " SELECT p.id AS id, principalinvestigator, c.id AS aaaclientid, person, m.id AS aaamanagerid,
                  contactperson, ctct.id AS aaacontactid, title, plateid, p.status, platereference, barcodeset,
                  species, tissue, sampletype, collectionmethod, weightconcentration, layoutfile, fragmentlength,
                  molarconcentration, labbookpage, protocol, p.comment as comment, p.user AS user, p.time AS time,
                  a.id AS analysisid, p.spikemolecules
               FROM #__aaaproject p
               LEFT JOIN #__aaaclient c ON p.#__aaaclientid = c.id
               LEFT JOIN #__aaacontact ctct ON p.#__aaacontactid = ctct.id
               LEFT JOIN #__aaamanager m ON p.#__aaamanagerid = m.id
               LEFT JOIN #__aaaanalysis a ON p.id = a.#__aaaprojectid
               WHERE p.id = '" . $searchid . "' ";

    $db->setQuery($query);
    $item = $db->loadObject();
    if (!empty($item->layoutfile)) {
      $target_path = $uploadsfolder . "/" . $item->layoutfile; 
    } else {
      $target_path = $uploadsfolder . "ingenfil"; 
    }

    if (is_file($target_path)) {
      $item->fileupload = 1;
    } else {
      $item->fileupload = -1;
    }

    $allowinqueue = true;
    if (strcmp($inqueue, "inqueue") == 0) {
      if ($item->barcodeset == "") {
        JError::raiseWarning('Message', JText::_('Analysis requires a barcode set!'));
        $allowinqueue = false;
      } else if ($item->fileupload == -1 && $item->species == "") {
        JError::raiseWarning('Message', JText::_( 'Analysis requires either a species or a valid Layout file!'));
        $allowinqueue = false;
      }
    }
    $newstatus = "";
    if (($allowinqueue && strcmp($inqueue, "inqueue") == 0) || (strcmp($inqueue, "ready") == 0))
      $newstatus = $inqueue;
    if ((strcmp($cancel, "no") == 0) || (strcmp($cancel, "yes") == 0))
      $newstatus = (strcmp($cancel, "no") == 0)? "ready" : "cancelled";
    if ($newstatus != "") {
      $query = " UPDATE #__aaaproject SET status='" . $newstatus . "' WHERE id='" . $searchid . "' ";
      $db->setQuery($query);
      if ($db->query()) {
        JError::raiseNotice('Message', JText::_('Status -> \'' . $newstatus . '\''));
        $item->status = $newstatus;
      } else {
        JError::raiseWarning('Message', JText::_( "[ " . $db->getErrorMsg() . " ]"));
      }
    }

    return $item;  
  }

  public function getAnalysis() {
  #######    READ in directory info from config file    ######################################
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
    $query = " SELECT id, #__aaaprojectid, lanecount, extraction_version, annotation_version, rpkm,
               genome, transcript_db_version, transcript_variant, comment, resultspath, emails, status
               FROM #__aaaanalysis WHERE #__aaaprojectid = '" . $searchid . "'  
               ORDER BY status DESC, id DESC ";

    $db->setQuery($query);
    $item = $db->loadObjectList();
//    if (!empty($item->layoutfile)) {
//      $target_path = $uploadsfolder . "/" . $item->layoutfile; 
//    } else {
//      $target_path = $uploadsfolder . "ingenfil"; 
//    }
//    if (is_file($target_path)) {
//      $item->fileupload = 1;
//    } else {
//      $item->fileupload = -1;
//    }
    return $item;
  }

  public function getTheAnalysis() {

    $db =& JFactory::getDBO();
    $analysid = JRequest::getVar('searchid') ;
    $query = " SELECT a.id AS id, #__aaaprojectid, extraction_version, annotation_version,
               genome, transcript_db_version, transcript_variant, a.comment AS comment,
               p.plateid AS plateid, barcodeset, resultspath, emails, a.status AS status, p.spikemolecules
               FROM #__aaaanalysis a, #__aaaproject p
               WHERE a.#__aaaprojectid = p.id
               AND a.id = '" . $analysid . "'  
                ";
    $db->setQuery($query);
    $item = $db->loadObject();
    return $item;
  }

  public function getSeqBatches() {
    $db =& JFactory::getDBO();
    $searchid = JRequest::getVar('searchid') ;
    $query = " SELECT b.id AS id, #__aaaprojectid, proj.title AS title, 
     plannednumberoflanes, COUNT(l.id) AS assignedlanes,
     #__aaasequencingprimerid, indexprimerid, b.title AS batchno,
     b.labbookpage AS labbookpage, plannednumberofcycles, cost, invoice, signed, plateid,
     GROUP_CONCAT(DISTINCT(r.id), '-', r.illuminarunid) AS illids,
     b.comment AS comment, b.user AS user, b.time AS time,
     primer.primername AS primer, indexp.primername AS indexprimer
      FROM #__aaasequencingbatch b
      LEFT JOIN #__aaaproject proj ON b.#__aaaprojectid = proj.id
      LEFT JOIN #__aaalane l ON l.#__aaasequencingbatchid = b.id
      LEFT JOIN #__aaailluminarun r ON l.#__aaailluminarunid = r.id
      LEFT OUTER JOIN #__aaasequencingprimer primer ON b.#__aaasequencingprimerid = primer.id
      LEFT OUTER JOIN #__aaasequencingprimer indexp ON b.indexprimerid = indexp.id
           WHERE #__aaaprojectid = '$searchid'  
       GROUP BY b.id
       ORDER BY b.id  ASC  ";
    $db->setQuery($query);
    $item = $db->loadObjectList();
    return $item;
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

	public function getTable($type = 'Project', $prefix = 'DbAppTable', $config = array()) {
		return JTable::getInstance($type, $prefix, $config);
	}

  public function getForm($data = array(), $loadData = true) {
    // Get the form.
    $form = $this->loadForm('com_dbapp.project', 'project', array('control' => 'jform', 'load_data' => $loadData));
    if (empty($form)) {
      return false;
    }
    return $form;
  }

  public function save($data = array()) {
    $db =& JFactory::getDBO();
JError::raiseWarning('500', JText::_('[from site/models/project.php]C ' . JRequest::getVar('c')));

    $db->updateObject('#__aaaproject', $data, 'id');
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
    $data = JFactory::getApplication()->getUserState('com_dbapp.edit.project.data', array());
    if (empty($data)) {
      $data = $this->getItem();
    }
    return $data;
  }



}
