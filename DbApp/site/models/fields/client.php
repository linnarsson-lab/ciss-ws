<?php
defined('_JEXEC') or die;
jimport('joomla.form.helper');
JFormHelper::loadFieldClass('list');

class JFormFieldClient extends JFormFieldList {
JError::raiseWarning('500', JText::_('[from amodels fields client '));

	protected $type = 'Client';

	protected function getOptions() {
		$db = JFactory::getDBO();
		$query = new JDatabaseQuery;
		$query->select('#__aaaclient.id as id, principalinvestigator, department, address, vatno, comment, user, time, #__categories.title as category, catid');
		$query->from('#__aaaclient');
		$query->leftJoin('#__categories on catid=#__categories.id');
		$db->setQuery((string)$query);
		$messages = $db->loadObjectList();

		$options = array();
		if ($messages) {
			foreach($messages as $message) {
				$options[] = JHtml::_('select.option', $message->id, $message->principalinvestigator, $message->department, $message->address, $message->vatno, 
        $message->comment, $message->user, $message->time, $message->category . ($message->catid ? ' (' . $message->category . ')' : ''));
			}
		}
		$options = array_merge(parent::getOptions(), $options);
		return $options;
	}
}
