<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelSequencingBatches extends JModel {

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
    $query = ' SELECT b.id AS id, #__aaaprojectid, p.title AS title,
                 plannednumberoflanes, #__aaasequencingprimerid, indexprimerid,
                 b.title AS batchno, b.labbookpage AS labbookpage, cl.principalinvestigator, cl.id AS clientid,
                 plannednumberofcycles, plannedindexcycles, cost, invoice, signed, plateid, p.id AS platedbid, 
                 GROUP_CONCAT(DISTINCT(r.illuminarunid) ORDER BY r.illuminarunid ASC) AS illids,
                 GROUP_CONCAT(DISTINCT(r.id) ORDER BY r.illuminarunid ASC) AS illdbids,
                 b.comment AS comment, b.user AS user, b.time AS time,
                 primer.primername AS primer, COUNT(l.id) AS assignedlanes,
                 indexp.primername AS indexprimer
      FROM #__aaasequencingbatch b
      LEFT JOIN #__aaaproject p ON b.#__aaaprojectid = p.id
      LEFT JOIN #__aaaclient cl ON cl.id = p.#__aaaclientid
      LEFT JOIN #__aaalane l ON l.#__aaasequencingbatchid = b.id
      LEFT JOIN #__aaailluminarun r ON l.#__aaailluminarunid = r.id
      LEFT OUTER JOIN #__aaasequencingprimer primer ON b.#__aaasequencingprimerid = primer.id
      LEFT OUTER JOIN #__aaasequencingprimer indexp ON b.indexprimerid = indexp.id
      WHERE b.id > 0 GROUP BY b.id ORDER BY b.id DESC
   ';
    $db->setQuery($query);
//    $statement = $db->loadResult();GROUP_CONCAT(DISTINCT(illuminarunid)) AS illuminaruns,
    $item = $db->loadObjectList();
    return $item;  

  }

}
