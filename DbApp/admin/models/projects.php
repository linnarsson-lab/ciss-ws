<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.modellist');

class DbAppModelProjects extends JModelList {

	protected function getListQuery() {
		// Create a new query object.
		$db = JFactory::getDBO();
		$query = $db->getQuery(true);
		// Select some fields
		$query->select('#__aaaproject.id AS id, title, platereference, plateid, species, tissue, sampletype, collectionmethod, 
                 weightconcentration, fragmentlength, molarconcentration, comment, user, time');
		// From the dbapp_one table
		$query->from('#__aaaproject');

		return $query;
	}
}
