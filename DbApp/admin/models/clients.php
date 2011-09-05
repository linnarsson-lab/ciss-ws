<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.modellist');

class DbAppModelClients extends JModelList {

	protected function getListQuery() {
		// Create a new query object.
		$db = JFactory::getDBO();
		$query = $db->getQuery(true);
		// Select some fields
		$query->select('#__aaaclient.id AS id, principalinvestigator, department, address, vatno, comment, user, time, #__categories.title AS category');
		// From the dbapp_one table
		$query->from('#__aaaclient');
    $query->leftJoin('#__categories on catid=#__categories.id');

		return $query;
	}

}
