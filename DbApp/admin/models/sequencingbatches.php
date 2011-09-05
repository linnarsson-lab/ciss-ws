<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.modellist');

class DbAppModelSequencingBatches extends JModelList {

	protected function getListQuery() {
		// Create a new query object.
		$db = JFactory::getDBO();
		$query = $db->getQuery(true);
		// Select some fields
		$query->select('id, plannednumberoflanes, plannednumberofcycles, cost, invoice, signed, comment, user, time');
		// From the dbapp_one table
		$query->from('#__aaasequencingbatch');
		return $query;
	}
}

