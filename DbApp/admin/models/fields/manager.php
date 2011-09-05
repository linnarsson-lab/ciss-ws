<?php
defined('_JEXEC') or die;
jimport('joomla.form.helper');
JFormHelper::loadFieldClass('list');

class JFormFieldManager extends JFormFieldList {

	protected $type = 'Manager';

	protected function getOptions() {
		$db = JFactory::getDBO();
		$query = new JDatabaseQuery;
		$query->select('id, person, email, phone, comment, user, time');
		$query->from('#__aaamanager');
		$db->setQuery((string)$query);
		$messages = $db->loadObjectList();

		$options = array();
		if ($messages) {
			foreach($messages as $message) {
				$options[] = JHtml::_('select.option', $message->id, $message->person, $message->email, $message->phone, $message->comment, $message->user, $message->time);
			}
		}
		$options = array_merge(parent::getOptions(), $options);
		return $options;
	}
}
