<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.modellist');

class DbAppModelLanes extends JModelList {

	protected function getListQuery() {
		// Create a new query object.
		$db = JFactory::getDBO();
		$query = $db->getQuery(true);
		// Select some fields
		$query->select('id, laneno, cycles, molarconcentration, yield, comment, user, time');
		// From the dbapp_one table
		$query->from('#__aaalane');
		return $query;
	}


}

