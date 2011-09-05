<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.modellist');

class DbAppModelSequencingPrimers extends JModelList {

	protected function getListQuery() {
		// Create a new query object.
		$db = JFactory::getDBO();
		$query = $db->getQuery(true);
		// Select some fields
		$query->select('id, primername, sequence, comment, user, time');
		// From the dbapp_one table
		$query->from('#__aaasequencingprimer');
		return $query;
	}
}

