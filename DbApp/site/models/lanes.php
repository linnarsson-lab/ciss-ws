<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.model');

class DbAppModelLanes extends JModel {

 /** Items total
   * @var integer
   */
  var $_total = null;
 
  /** Pagination object
   * @var object
   */
  var $_pagination = null;

	protected $item;

  function __construct() {
    parent::__construct();
    global $mainframe, $option;
    $mainframe = JFactory::getApplication();
       // Get pagination request variables
    $limit = $mainframe->getUserStateFromRequest('global.list.limit', 'limit', $mainframe->getCfg('list_limit'), 'int');
    $limitstart = JRequest::getVar('limitstart', 0, '', 'int');
         // In case limit has been changed, adjust it
    $limitstart = ($limit != 0 ? (floor($limitstart / $limit) * $limit) : 0);
    $this->setState('limit', $limit);
    $this->setState('limitstart', $limitstart);
  }

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

  /** Returns the query
  * @return string The query to be used to retrieve the rows from the database
  */
  function _buildQuery() {
    $query = " SELECT l.id as id, r.id as aaailluminarunid, illuminarunid, b.id as aaasequencingbatchid,
                      laneno, cycles, l.molarconcentration, yield, l.status AS Lstatus, l.comment as comment,
                      l.user as user, l.time as time, title, plateid, rundate
               FROM #__aaalane l
               LEFT JOIN #__aaailluminarun r ON l.#__aaailluminarunid = r.id
               LEFT JOIN #__aaasequencingbatch b ON l.#__aaasequencingbatchid = b.id
               LEFT JOIN #__aaaproject p ON b.#__aaaprojectid = p.id
               ORDER BY l.id ";
    return $query;
  }

  function getData() {
        // if data hasn't already been obtained, load it
    if (empty($this->_data)) {
      $query = $this->_buildQuery();
      $this->_data = $this->_getList($query, $this->getState('limitstart'), $this->getState('limit')); 
    }
    return $this->_data;
  }

  function getTotal() {
        // Load the content if it doesn't already exist
   if (empty($this->_total)) {
      $query = $this->_buildQuery();
      $this->_total = $this->_getListCount($query);    
    }
    return $this->_total;
  }

  function getPagination() {
        // Load the content if it doesn't already exist
    if (empty($this->_pagination)) {
      jimport('joomla.html.pagination');
      $this->_pagination = new JPagination($this->getTotal(), $this->getState('limitstart'), $this->getState('limit') );
    }
    return $this->_pagination;
  }

  public function getItems() {
    $db =& JFactory::getDBO();
    $selector = "ORDER BY l.id ";
    $projectid = JRequest::getVar('projectid', 0);
    $runid = JRequest::getVar('runid', 0);
    if ($projectid != 0) {
       $selector = " WHERE p.id=" . $db->Quote($projectid) . " ORDER BY batchtitle, laneno ASC ";
    } else if ($runid != 0) {
       $selector = " WHERE r.id=" . $db->Quote($runid) . " ORDER BY laneno ASC ";
    }
    $query = " SELECT l.id as id, r.id as aaailluminarunid, illuminarunid, b.id as aaasequencingbatchid,
                      laneno, r.cycles, l.molarconcentration, yield, l.status AS Lstatus, l.comment as comment,
                      l.user as user, l.time as time, p.title, plateid, rundate, b.title AS batchtitle,
                      m.email AS email, m.person AS person, p.id AS Pid, r.status AS runstatus,
                      p.species, p.layoutfile, p.barcodeset
               FROM #__aaalane l 
               LEFT JOIN #__aaailluminarun r ON l.#__aaailluminarunid = r.id
               LEFT JOIN #__aaasequencingbatch b ON l.#__aaasequencingbatchid = b.id
               LEFT JOIN #__aaaproject p ON b.#__aaaprojectid = p.id
               LEFT JOIN #__aaamanager m ON p.#__aaamanagerid = m.id
               $selector ";
    $db->setQuery($query);
    $items = $db->loadObjectList();
    return $items;
  }

}
